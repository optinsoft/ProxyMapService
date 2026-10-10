using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using static ProxyMapService.Proxy.Utils.CacheUtils;
using static ProxyMapService.Proxy.Utils.HttpBodyUtils;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Tunnels
{
    public class HttpRequestTunnel : HttpTunnel
    {
        private readonly HttpParser _httpParser;

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
            _httpParser = new HttpParser(false, _context.RequestHeader == null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpParser.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override async Task ProcessAsync(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            var parser = _httpParser;
            if (_selfState.ResetReadHeaders)
            {
                _selfState.ResetReadHeaders = false;
                if (_readCounter.IsLogReading)
                {
                    LogTunnelResetReadingHeaders(_context.Logger, _selfState.TunnelId);
                }
                _context.RequestHeader = null;
                _context.ResponseHeader = null;
                _context.DisposeRequestBodyTracker();
                _context.DisposeResponseBodyTracker();
                parser.Reset();
            }
            if (!_otherTunnelState.ResetReadHeaders)
            {
                if (_readCounter.IsLogReading)
                {
                    LogOtherTunnelResetReadingHeaders(_context.Logger,
                        _selfState.TunnelId, _otherTunnelState.TunnelId);
                }
                _otherTunnelState.ResetReadHeaders = true;
            }
            if (parser.ReadingHeaders)
            {
                if (_readCounter.IsLogReading)
                {
                    LogTunnelReadingHeaders(
                        _context.Logger,
                        _selfState.TunnelId,
                        StreamDirectionName.GetName(_readCounter.Direction));
                }
                parser.AppendData(data.Span, out bool endOfHeaders);
                if (!endOfHeaders)
                {
                    return;
                }
                bool headerModified = false;
                CacheEntry? requestCacheEntry = null;
                var headerAndTail = parser.HeaderAndTail;
                if (headerAndTail != null)
                {
                    if (_readCounter.IsLogReading)
                    {
                        LogTunnelHeadersRead(
                            _context.Logger,
                            _selfState.TunnelId,
                            StreamDirectionName.GetName(_readCounter.Direction));
                    }
                    if (_context.ResponseHeader != null)
                    {
                        _context.Logger.LogNotNullHTTPResponseHeader(_context.Host);
                        //Debug.Assert(false, "HTTP Response Header is not null");
                    }
                    var header = new HttpRequestHeader(headerAndTail.HeaderLines);
                    _context.RequestHeader = header;
                    _context.RequestHeadersLogger?.OnHttpRequestHeader(_context, header);
                    if (!header.BadRequest)
                    {
                        _context.SessionManager.NotifyIfRequestUrlMatches(_context, header, _selfState.IsSecure);
                        _context.RequestBodyTracker = CreateRequestBodyTracker(_context, header, null);
                        if (headerAndTail.TailBytes != null)
                        {
                            _context.RequestBodyTracker.TryAppend(headerAndTail.TailBytes);
                        }
                    }
                    requestCacheEntry = await GetCacheEntry(_context, header);
                    if (_context.Host.Overwritten)
                    {
                        if (headerAndTail.HeaderLines.Length > 0)
                        {
                            var modifiedFirstLine = HttpHeaderRewriter.OverrideHttpCommandHost(headerAndTail.HeaderLines[0], _context.Host);
                            if (modifiedFirstLine != null)
                            {
                                headerAndTail.HeaderLines[0] = modifiedFirstLine;
                                headerModified = true;
                            }
                        }
                        if (HttpHeaderRewriter.OverrideHostHeader(headerAndTail.HeaderLines, _context.Host))
                        {
                            headerModified = true;
                        }
                    }
                }
                else
                {
                    if (_readCounter.IsLogReading)
                    {
                        LogTunnelBodyRead(
                            _context.Logger,
                            _selfState.TunnelId,
                            StreamDirectionName.GetName(_readCounter.Direction));
                    }
                    _context.RequestBodyTracker?.TryAppend(data.Span);
                }
                using var cacheFileStream = requestCacheEntry != null ? GetCacheEntryFileStream(requestCacheEntry) : null;
                if (requestCacheEntry != null && cacheFileStream != null)
                {
                    _selfState.ResetReadHeaders = true;
                    await ReplyCacheFile(requestCacheEntry, cacheFileStream, _source, _context);
                }
                else
                {
                    if (_sendCounter.IsLogSending)
                    {
                        LogTunnelSending(
                            _context.Logger,
                            _selfState.TunnelId,
                            StreamDirectionName.GetName(_sendCounter.Direction));
                    }
                    await _destinationReady.Task.WaitAsync(token);
                    if (headerAndTail != null && headerModified)
                    {
                        await SendModifiedHeadersAndBody(_destination, headerAndTail.HeaderLines, headerAndTail.TailBytes, token);
                    }
                    else
                    {
                        await parser.WriteHeaderStreamAsync(_destination, token);
                    }
                }
                parser.ClearHeaderStream();
            }
            else
            {
                if (_readCounter.IsLogReading)
                {
                    LogTunnelBodyRead(
                        _context.Logger,
                        _selfState.TunnelId,
                        StreamDirectionName.GetName(_readCounter.Direction));
                }
                _context.RequestBodyTracker?.TryAppend(data.Span);
                if (_sendCounter.IsLogSending)
                {
                    LogTunnelSending(
                        _context.Logger,
                        _selfState.TunnelId,
                        StreamDirectionName.GetName(_sendCounter.Direction));
                }
                await _destinationReady.Task.WaitAsync(token);
                await _destination.WriteAsync(data, token);
            }
        }
    }
}
