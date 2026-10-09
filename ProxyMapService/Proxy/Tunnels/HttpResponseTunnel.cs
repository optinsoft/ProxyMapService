using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using System.Diagnostics;
using static ProxyMapService.Proxy.Utils.CacheUtils;
using static ProxyMapService.Proxy.Utils.HttpBodyUtils;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Tunnels
{
    public class HttpResponseTunnel : HttpTunnel
    {
        private readonly HttpParser _httpParser;

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
            _httpParser = new HttpParser(true, _context.ResponseHeader == null);
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
                _context.ResponseCacheEntry = null;
                _context.DisposeResponseCacheFileStream();
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
                        if (_context.RequestHeader == null)
                        {
                            _context.Logger.LogNullHTTPRequestHeader(_context.Host);
                            //Debug.Assert(false, "HTTP Request Header is null");
                        }
                        _context.ResponseHeader = new HttpResponseHeader(headerAndBody.HeaderLines, _httpParser.HeaderLength);
                        _context.ResponseHeadersLogger?.OnHttpHeader(_context, _context.ResponseHeader);
                        if (!_context.ResponseHeader.BadResponse)
                        {
                            CreateResponseBodyTracker(_context, _context.ResponseHeader, headerAndBody.BodyBytes, null);
                            if (CreateResponseCacheFileStream(_context, _context.RequestHeader))
                            {
                                if (_context.ResponseCacheFileStream != null)
                                {
                                    await _httpParser.CopyHeaderStreamAsync(_context.ResponseCacheFileStream);
                                    await HandleEndOfResponseCacheFileStream(_context);
                                }
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
                        _context.ResponseBodyTracker?.TryAppend(data.Span);
                        if (_context.ResponseCacheFileStream != null)
                        {
                            if (_context.ResponseCacheEntry != null)
                            {
                                await _context.ResponseCacheFileStream.WriteAsync(data);
                                await HandleEndOfResponseCacheFileStream(_context);
                            }
                            else
                            {
                                //context.Logger.LogDebug("Response cache entry is null (1)");
                                Debug.Assert(false, "Response cache entry is null (1)");
                                _context.DisposeResponseCacheFileStream();
                            }
                        }
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
                _context.ResponseBodyTracker?.TryAppend(data.Span);
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
            }
        }
    }
}