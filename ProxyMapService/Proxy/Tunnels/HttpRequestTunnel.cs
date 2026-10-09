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
                _httpParser.Reset();
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
            if (_httpParser.ReadingHeaders)
            {
                if (_readCounter.IsLogReading)
                {
                    LogTunnelReadingHeaders(
                        _context.Logger,
                        _selfState.TunnelId,
                        StreamDirectionName.GetName(_readCounter.Direction));
                }
                _httpParser.AppendData(data.Span, out bool endOfHeaders);
                if (endOfHeaders)
                {
                    bool headerModified = false;
                    CacheEntry? requestCacheEntry = null;
                    var headerAndBody = _httpParser.HeaderAndBody;
                    if (headerAndBody != null)
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
                        _context.RequestHeader = new HttpRequestHeader(headerAndBody.HeaderLines);
                        _context.RequestHeadersLogger?.OnHttpHeader(_context, _context.RequestHeader);
                        if (!_context.RequestHeader.BadRequest)
                        {
                            _context.SessionManager.NotifyIfRequestUrlMatches(_context, _context.RequestHeader, _selfState.IsSecure);
                            CreateRequestBodyTracker(_context, _context.RequestHeader, headerAndBody.BodyBytes, null);
                        }
                        requestCacheEntry = await GetCacheEntry(_context, _context.RequestHeader);
                        if (_context.Host.Overwritten)
                        {
                            if (headerAndBody.HeaderLines.Length > 0)
                            {
                                var modifiedFirstLine = HttpHeaderRewriter.OverrideHttpCommandHost(headerAndBody.HeaderLines[0], _context.Host);
                                if (modifiedFirstLine != null)
                                {
                                    headerAndBody.HeaderLines[0] = modifiedFirstLine;
                                    headerModified = true;
                                }
                            }
                            if (HttpHeaderRewriter.OverrideHostHeader(headerAndBody.HeaderLines, _context.Host))
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
                        if (headerAndBody != null && headerModified)
                        {
                            await SendModifiedHeadersAndBody(_destination, headerAndBody.HeaderLines, headerAndBody.BodyBytes, token);
                        }
                        else
                        {
                            await _httpParser.WriteHeaderStreamAsync(_destination, token);
                        }
                    }
                    _httpParser.ClearHeaderStream();
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
