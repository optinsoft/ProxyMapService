using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using System.Text;
using static ProxyMapService.Proxy.Utils.CacheUtils;
using static ProxyMapService.Proxy.Utils.HttpBodyUtils;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Tunnels
{
    public class HttpRequestTunnel : HttpTunnel
    {
        private readonly HttpParser _parser;

        private bool _protocolDetected;

        private int _bytesInPrefaceBuffer = 0;
        private byte[]? _rolledBackBytes = null;

        private static readonly byte[] Http2Preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();

        public HttpRequestTunnel(
            SessionContext context,
            CountingStream source,
            CountingStream destination,
            TaskCompletionSource destinationReady,
            BytesReadCounter readCounter,
            BytesSendCounter sendCounter,
            TunnelState selfState,
            TunnelState otherTunnelState)
            : base(
            context,
            source,
            destination,
            destinationReady,
            readCounter,
            sendCounter,
            selfState,
            otherTunnelState)
        {
            _parser = new HttpParser(false, _context.RequestHeader == null);
            _protocolDetected = context.IncomingHttp2 || (context.Http != null && context.Http.HTTPVerb != "CONNECT");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _parser.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override async Task ProcessAsync(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            if (!_protocolDetected)
            {
                var prefaceResult = TryDetectHttp2(ref data);
                if (prefaceResult == PrefaceResult.WaitingForMoreData)
                {
                    return;
                }

                _context.IncomingHttp2 = (prefaceResult == PrefaceResult.IsHttp2);
                _protocolDetected = true;

                if (_rolledBackBytes != null)
                {
                    var combined = new byte[_rolledBackBytes.Length + data.Length];
                    _rolledBackBytes.CopyTo(combined, 0);
                    data.CopyTo(combined.AsMemory(_rolledBackBytes.Length));

                    data = combined;
                    _rolledBackBytes = null;
                }

                if (_context.IncomingHttp2)
                {
                    _context.Logger.LogDetectedClientHTTP2(_context.IncomingEndPoint);
                }
            }

            while (data.Length > 0)
            {
                if (_selfState.ResetReadHeaders)
                {
                    _selfState.ResetReadHeaders = false;
                    if (_readCounter.IsLogReading)
                    {
                        LogTunnelResetReadingHeaders(_context.Logger, _selfState.TunnelId);
                    }
                    _context.RequestHeader = null;
                    _context.DisposeRequestBodyTracker();
                    _parser.Reset();
                }
                if (_parser.ReadingHeaders)
                {
                    data = await ProcessReadHeaderAsync(data, token);
                }
                else
                {
                    data = await ProcessReadBodyAsync(data, _context.RequestBodyTracker, token);
                }
                if (_context.RequestBodyTracker?.Completed == true)
                {
                    _selfState.ResetReadHeaders = true;
                }
            }
        }

        protected async Task<ReadOnlyMemory<byte>> ProcessReadHeaderAsync(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            if (_readCounter.IsLogReading)
            {
                LogTunnelReadingHeaders(
                    _context.Logger,
                    _selfState.TunnelId,
                    StreamDirectionName.GetName(_readCounter.Direction));
            }
            _parser.AppendData(data.Span, out bool endOfHeaders);
            if (!endOfHeaders)
            {
                return data[data.Length..];
            }

            var headerAndTail = _parser.GetHeaderLinesAndTail();
            if (headerAndTail == null)
            {
                await SendRawAsync(_parser.BufferedBytes, token);
                return data[data.Length..];
            }

            _parser.ClearHeaderStream();

            if (_readCounter.IsLogReading)
            {
                LogTunnelHeadersRead(
                    _context.Logger,
                    _selfState.TunnelId,
                    StreamDirectionName.GetName(_readCounter.Direction));
            }

            var requestHeader = new HttpRequestHeader(headerAndTail.HeaderLines);
            _context.RequestHeader = requestHeader;
            _context.RequestHeadersLogger?.OnHttpRequestHeader(_context, requestHeader);

            if (requestHeader.BadRequest)
            {
                await SendRawAsync(_parser.BufferedBytes, token);
                return data[data.Length..];
            }

            _context.SessionManager.NotifyIfRequestUrlMatches(_context, requestHeader, _selfState.IsSecure);
            var tracker = CreateRequestBodyTracker(_context, requestHeader, null);

            var requestCacheEntry = await GetCacheEntry(_context, requestHeader);

            if (_context.Host.Overwritten)
            {
                if (headerAndTail.HeaderLines.Length > 0)
                {
                    var modifiedFirstLine = HttpHeaderRewriter.OverrideHttpCommandHost(headerAndTail.HeaderLines[0], _context.Host);
                    if (modifiedFirstLine != null)
                    {
                        headerAndTail.HeaderLines[0] = modifiedFirstLine;
                    }
                }
                HttpHeaderRewriter.OverrideHostHeader(headerAndTail.HeaderLines, _context.Host);
            }

            using var cacheFileStream = requestCacheEntry != null ? GetCacheEntryFileStream(requestCacheEntry) : null;

            if (requestCacheEntry != null && cacheFileStream != null)
            {
                if (headerAndTail.TailBytes != null)
                {
                    _context.Logger.LogTailIsNotNull();
                    //Debug.Assert(false, "!!! Tail is not null !!!");
                }
                await ReplyCacheFile(requestCacheEntry, cacheFileStream, _source, _context, _context.RequestHeader);
            }
            else
            {
                await SendHeaderAsync(headerAndTail.HeaderLines, token);
            }

            if (headerAndTail.TailBytes == null)
            {
                return data[data.Length..];
            }
            
            if (_context.RequestBodyTracker?.Completed == true)
            {
                return headerAndTail.TailBytes;
            }
                
            if (tracker.TryAppend(headerAndTail.TailBytes, out var consumed) == true)
            {
                if (consumed > 0)
                {
                    var body = headerAndTail.TailBytes[..consumed];
                    await SendBodyAsync(body, token);
                }
                return headerAndTail.TailBytes.AsMemory()[consumed..];
            }

            return headerAndTail.TailBytes;
        }

        protected async Task<ReadOnlyMemory<byte>> ProcessReadBodyAsync(ReadOnlyMemory<byte> data, IBodyTracker? tracker, CancellationToken token)
        {
            if (tracker?.Completed == true)
            {
                _selfState.ResetReadHeaders = true;
                return data;
            }

            if (_readCounter.IsLogReading)
            {
                LogTunnelBodyRead(
                    _context.Logger,
                    _selfState.TunnelId,
                    StreamDirectionName.GetName(_readCounter.Direction));
            }

            if (tracker?.TryAppend(data.Span, out var consumed) == true)
            {
                if (consumed > 0)
                {
                    var body = data[..consumed];
                    await SendBodyAsync(body, token);
                }
                return data[consumed..];
            }

            await SendRawAsync(data, token);
            return data[data.Length..];
        }

        private enum PrefaceResult { WaitingForMoreData, IsHttp1, IsHttp2 }

        private PrefaceResult TryDetectHttp2(ref ReadOnlyMemory<byte> data)
        {
            int availableToCompare = Math.Min(data.Length, Http2Preface.Length - _bytesInPrefaceBuffer);

            if (availableToCompare > 0)
            {
                var incomingSpan = data.Span.Slice(0, availableToCompare);
                var expectedSpan = Http2Preface.AsSpan(_bytesInPrefaceBuffer, availableToCompare);

                if (!incomingSpan.SequenceEqual(expectedSpan))
                {
                    if (_bytesInPrefaceBuffer > 0)
                    {
                        _rolledBackBytes = new byte[_bytesInPrefaceBuffer];
                        Http2Preface.AsSpan(0, _bytesInPrefaceBuffer).CopyTo(_rolledBackBytes);
                    }

                    return PrefaceResult.IsHttp1;
                }

                data = data.Slice(availableToCompare);
                _bytesInPrefaceBuffer += availableToCompare;
            }

            if (_bytesInPrefaceBuffer == Http2Preface.Length)
            {
                if (_bytesInPrefaceBuffer > 0)
                {
                    _rolledBackBytes = new byte[_bytesInPrefaceBuffer];
                    Http2Preface.AsSpan(0, _bytesInPrefaceBuffer).CopyTo(_rolledBackBytes);
                }

                return PrefaceResult.IsHttp2;
            }

            return PrefaceResult.WaitingForMoreData;
        }
    }
}