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
        }

        public async Task RunAsync()
        {
            var buffer = new byte[BufferSize];
            using var httpParser = new HttpParser(false, _context.RequestHeader == null);

            CancellationToken token = _context.Token;

            bool reading = false;

            try
            {
                int bytesRead;
                do
                {
                    if (_readCounter.IsLogReading)
                    {
                        LogTunnelReading(_context.Logger, _selfState.TunnelId, StreamDirectionName.GetName(_readCounter.Direction));
                    }
                    reading = true;
                    bytesRead = await _source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    reading = false;
                    if (bytesRead > 0)
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
                            httpParser.Reset();
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
                        if (httpParser.ReadingHeaders)
                        {
                            if (_readCounter.IsLogReading)
                            {
                                LogTunnelReadingHeaders(_context.Logger,
                                    _selfState.TunnelId, StreamDirectionName.GetName(_readCounter.Direction));
                            }
                            httpParser.AppendData(buffer.AsSpan(0, bytesRead), out bool endOfHeaders);
                            if (endOfHeaders)
                            {
                                bool headerModified = false;
                                CacheEntry? requestCacheEntry = null;
                                var headerAndBody = httpParser.HeaderAndBody;
                                if (headerAndBody != null)
                                {
                                    if (_readCounter.IsLogReading)
                                    {
                                        LogTunnelHeadersRead(_context.Logger,
                                            _selfState.TunnelId, StreamDirectionName.GetName(_readCounter.Direction));
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
                                    requestCacheEntry = await GetCacheEntry(_context);
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
                                        LogTunnelBodyRead(_context.Logger,
                                            _selfState.TunnelId, StreamDirectionName.GetName(_readCounter.Direction));
                                    }
                                    _context.RequestBodyTracker?.TryAppend(buffer.AsSpan(0, bytesRead));
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
                                        LogTunnelSending(_context.Logger,
                                            _selfState.TunnelId, StreamDirectionName.GetName(_sendCounter.Direction));
                                    }
                                    await _destinationReady.Task;
                                    if (headerAndBody != null && headerModified)
                                    {
                                        await SendModifiedHeadersAndBody(_destination, headerAndBody.HeaderLines, headerAndBody.BodyBytes, token);
                                    }
                                    else
                                    {
                                        await httpParser.WriteHeaderStreamAsync(_destination, token);
                                    }
                                }
                                httpParser.ClearHeaderStream();
                            }
                        }
                        else
                        {
                            if (_readCounter.IsLogReading)
                            {
                                LogTunnelBodyRead(_context.Logger,
                                    _selfState.TunnelId, StreamDirectionName.GetName(_readCounter.Direction));
                            }
                            _context.RequestBodyTracker?.TryAppend(buffer.AsSpan(0, bytesRead));
                            if (_sendCounter.IsLogSending)
                            {
                                LogTunnelSending(_context.Logger,
                                    _selfState.TunnelId, StreamDirectionName.GetName(_sendCounter.Direction));
                            }
                            await _destinationReady.Task;
                            await _destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        }
                    }
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException ex)
            {
                if (_readCounter.IsLogReading)
                {
                    LogTunnelDebugError(_context.Logger, ex.GetType().Name, ex.Message);
                }
            }
            catch (IOException ex)
            {
                if (_readCounter.IsLogReading)
                {
                    LogTunnelDebugError(_context.Logger, ex.GetType().Name, ex.Message);
                }
                if (reading)
                {
                    _source.OnDisconnected();
                }
            }
            catch (Exception ex)
            {
                LogTunnelError(_context.Logger, ex.GetType().Name, ex.Message, ex.StackTrace);
            }
        }
    }
}
