using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using System.Diagnostics;
using System.Text;
using static ProxyMapService.Proxy.Utils.CacheUtils;
using static ProxyMapService.Proxy.Utils.HttpBodyUtils;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Tunnels
{
    public class HttpResponseTunnel : HttpTunnel
    {
        private readonly HttpParser _parser;

        public HttpResponseTunnel(
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
            _parser = new HttpParser(true, _context.ResponseHeader == null);
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
                    _context.ResponseHeader = null;
                    _context.DisposeResponseBodyTracker();
                    _context.ResponseCacheEntry = null;
                    _context.DisposeResponseCacheFileStream();
                    _parser.Reset();
                }
                if (_parser.ReadingHeaders)
                {
                    data = await ProcessReadHeaderAsync(data, token);
                }
                else
                {
                    data = await ProcessReadBodyAsync(data, _context.ResponseBodyTracker, token);
                }
                if (_context.ResponseBodyTracker?.Completed == true)
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

            var responseHeader = new HttpResponseHeader(headerAndTail.HeaderLines, _parser.HeaderLength);
            _context.ResponseHeader = responseHeader;
            _context.RequestHeaderHistory.TryGetValue(responseHeader.ResponseNum, out var requestHeader);
            if (requestHeader != null)
            {
                responseHeader.ResponseId = requestHeader.RequestId;
            }
            _context.ResponseHeadersLogger?.OnHttpResponseHeader(_context, responseHeader, requestHeader);

            if (responseHeader.BadResponse)
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

            var tracker = CreateResponseBodyTracker(_context, responseHeader, requestHeader);

            CreateResponseCacheFileStream(_context, requestHeader);

            bool sendLogged = false;

            string header = string.Join("\r\n", headerAndTail.HeaderLines);
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);

            if (_context.ResponseCacheFileStream != null)
            {
                await _context.ResponseCacheFileStream.WriteAsync(headerBytes, CancellationToken.None);
                await HandleEndOfResponseCacheFileStream(_context);
            }

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

            if (headerAndTail.TailBytes == null)
            {
                return data[data.Length..];
            }

            if (tracker.Completed == true)
            {
                return headerAndTail.TailBytes;
            }

            if (tracker.TryAppend(headerAndTail.TailBytes, out var consumed) == true)
            {
                if (consumed > 0)
                {
                    var body = headerAndTail.TailBytes[..consumed];
                    if (_context.ResponseCacheFileStream != null)
                    {
                        await _context.ResponseCacheFileStream.WriteAsync(body, CancellationToken.None);
                        await HandleEndOfResponseCacheFileStream(_context);
                    }
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
                    if (_context.ResponseCacheFileStream != null)
                    {
                        if (_context.ResponseCacheEntry != null)
                        {
                            await _context.ResponseCacheFileStream.WriteAsync(body);
                            await HandleEndOfResponseCacheFileStream(_context);
                        }
                        else
                        {
                            //context.Logger.LogDebug("Response cache entry is null (1)");
                            Debug.Assert(false, "Response cache entry is null (1)");
                            _context.DisposeResponseCacheFileStream();
                        }
                    }
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


            if (_context.ResponseCacheFileStream != null)
            {
                if (_context.ResponseCacheEntry != null)
                {
                    await _context.ResponseCacheFileStream.WriteAsync(data);
                    await HandleEndOfResponseCacheFileStream(_context);
                }
                else
                {
                    //context.Logger.LogDebug("Response cache entry is null (2)");
                    Debug.Assert(false, "Response cache entry is null (2)");
                    _context.DisposeResponseCacheFileStream();
                }
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