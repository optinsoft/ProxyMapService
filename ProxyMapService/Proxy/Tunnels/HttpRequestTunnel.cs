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
                if (_sendCounter.IsLogSending)
                {
                    LogTunnelSending(
                        _context.Logger,
                        _selfState.TunnelId,
                        StreamDirectionName.GetName(_sendCounter.Direction));
                }
                await _destinationReady.Task.WaitAsync(token);
                await _destination.WriteAsync(_parser.BufferedBytes, token);
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
                if (_sendCounter.IsLogSending)
                {
                    LogTunnelSending(
                        _context.Logger,
                        _selfState.TunnelId,
                        StreamDirectionName.GetName(_sendCounter.Direction));
                }
                await _destinationReady.Task.WaitAsync(token);
                await _destination.WriteAsync(_parser.BufferedBytes, token);
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

            bool sendLogged = false;

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
                string header = string.Join("\r\n", headerAndTail.HeaderLines);
                byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                if (_sendCounter.IsLogSending && !sendLogged)
                {
                    sendLogged = true;
                    LogTunnelSending(
                        _context.Logger,
                        _selfState.TunnelId,
                        StreamDirectionName.GetName(_sendCounter.Direction));
                }
                await _destinationReady.Task.WaitAsync(token);
                await _destination.WriteAsync(headerBytes.AsMemory(0, headerBytes.Length), token);
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
                    if (_sendCounter.IsLogSending && !sendLogged)
                    {
                        LogTunnelSending(
                            _context.Logger,
                            _selfState.TunnelId,
                            StreamDirectionName.GetName(_sendCounter.Direction));
                    }
                    await _destinationReady.Task.WaitAsync(token);
                    await _destination.WriteAsync(body, token);
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
                    if (_sendCounter.IsLogSending)
                    {
                        LogTunnelSending(
                            _context.Logger,
                            _selfState.TunnelId,
                            StreamDirectionName.GetName(_sendCounter.Direction));
                    }
                    await _destinationReady.Task.WaitAsync(token);
                    await _destination.WriteAsync(body, token);
                }
                return data[consumed..];
            }

            if (_sendCounter.IsLogSending)
            {
                LogTunnelSending(
                    _context.Logger,
                    _selfState.TunnelId,
                    StreamDirectionName.GetName(_sendCounter.Direction));
            }

            await _destinationReady.Task.WaitAsync(token);
            await _destination.WriteAsync(data, token);
            return data[data.Length..];
        }
    }
}