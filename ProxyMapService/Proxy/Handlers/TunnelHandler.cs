using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ssl;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public partial class TunnelHandler : BaseResponseCacheHandler, IHandler
    {
        private static readonly TunnelHandler Self = new();
        private const int BufferSize = 8192;

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && context.OutgoingStream != null && !context.Token.IsCancellationRequested)
            {
                using SslStream? incomingSslStream = context.DecryptSSL ? context.SslMode switch {
                    SslMode.Yes => new(context.IncomingStream),
                    SslMode.Auto => await context.IncomingStream.IsTLS(context.Token) ? new(context.IncomingStream) : null,
                    _ => null
                } : null;
                using SslStream? outgoingSslStream = context.DecryptSSL ? context.UpstreamSslMode switch
                {
                    SslMode.Yes => new(context.OutgoingStream),
                    SslMode.Auto => NetworkSecurityHelper.IsStandardTlsPort(context.Host.Port) ? new(context.OutgoingStream) : null,
                    _ => null
                } : null;

                using CountingStream? incomingSslCountingStream =
                    incomingSslStream != null
                    ? new CountingStream(incomingSslStream, context,
                        context.ProxyCounters.IncomingReadSslCounter, context.ProxyCounters.IncomingSendSslCounter,
                        context.IncomingStream.ReadTunnelId, context.IncomingStream.SendTunnelId)
                    : null;
                using CountingStream? outgoingSslCountingStream =
                    outgoingSslStream != null
                    ? new CountingStream(outgoingSslStream, context,
                        context.ProxyCounters.OutgoingReadSslCounter, context.ProxyCounters.OutgoingSendSslCounter,
                        context.OutgoingStream.ReadTunnelId, context.OutgoingStream.SendTunnelId)
                    : null;

                var incomingReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var outgoingReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                var incomingStream = incomingSslCountingStream ?? context.IncomingStream;
                var outgoingStream = outgoingSslCountingStream ?? context.OutgoingStream;

                var requestTunnelTask = RequestTunnel(context, incomingSslStream,
                    incomingStream, outgoingStream, incomingReady, outgoingReady);
                var responseTunnelTask = ResponseTunnel(context, outgoingSslStream,
                    incomingStream, outgoingStream, incomingReady, outgoingReady);

                await Task.WhenAny(requestTunnelTask, responseTunnelTask);
            }

            return HandleStep.Terminate;
        }

        public static TunnelHandler Instance()
        {
            return Self;
        }

        #region High-Performance Logging

        [LoggerMessage(
            EventId = 1301,
            Level = LogLevel.Error,
            Message = "[RequestTunnel] {ExceptionName}: {ErrorMessage}")]
        private static partial void LogRequestTunnelError(ILogger logger, string exceptionName, string errorMessage);

        [LoggerMessage(
            EventId = 1302,
            Level = LogLevel.Error,
            Message = "[ResponseTunnel] {ExceptionName}: {ErrorMessage}")]
        private static partial void LogResponseTunnelError(ILogger logger, string exceptionName, string errorMessage);

        [LoggerMessage(
            EventId = 1303,
            Level = LogLevel.Error,
            Message = "[Tunnel] {ExceptionName}: {ErrorMessage}\n{StackTrace}")]
        private static partial void LogTunnelError(ILogger logger, string exceptionName, string errorMessage, string? stackTrace);

        [LoggerMessage(
            EventId = 1304,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: reading from {direction}...")]
        private static partial void LogTunnelReading(ILogger logger, long tunnelId, string direction);

        [LoggerMessage(
            EventId = 1305,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Reset reading headers")]
        private static partial void LogTunnelResetReadingHeaders(ILogger logger, long tunnelId);

        [LoggerMessage(
            EventId = 1306,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Resetting other tunnel ({otherTunnelId}) reading headers")]
        private static partial void LogOtherTunnelResetReadingHeaders(ILogger logger, long tunnelId, long otherTunnelId);

        [LoggerMessage(
            EventId = 1307,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Reading headers from {direction}...")]
        private static partial void LogTunnelReadingHeaders(ILogger logger, long tunnelId, string direction);

        [LoggerMessage(
            EventId = 1308,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Headers read from {direction}")]
        private static partial void LogTunnelHeadersRead(ILogger logger, long tunnelId, string direction);

        [LoggerMessage(
            EventId = 1309,
            Level = LogLevel.Debug,
            Message = "[Tunnel] {ExceptionName}: {ErrorMessage}")]
        private static partial void LogTunnelDebugError(ILogger logger, string exceptionName, string errorMessage);

        [LoggerMessage(
            EventId = 1309,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Body read from {direction}")]
        private static partial void LogTunnelBodyRead(ILogger logger, long tunnelId, string direction);

        [LoggerMessage(
            EventId = 1310,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: sending to {direction}...")]
        private static partial void LogTunnelSending(ILogger logger, long tunnelId, string direction);

        #endregion

        private static async Task RequestTunnel(SessionContext context,
            SslStream? incomingSslStream, CountingStream incomingStream, CountingStream outgoingStream,
            TaskCompletionSource incomingReady, TaskCompletionSource outgoingReady)
        {
            try
            {
                if (incomingSslStream != null)
                {
                    string subjectName = $"CN=*.{context.Host.OriginalHostname}";
                    X509Certificate2? serverCertificate = context.ServerCertificate;
                    if (serverCertificate == null)
                    {
                        if (context.CACertificate != null)
                        {
                            serverCertificate = SslOptionsFactory.CreateSignedCertificate(subjectName, context.Host.OriginalHostname, context.CACertificate);
                        }
                        else
                        {
                            throw new NullServerCertificateException();
                        }
                    }
                    var sslServerOptions = SslOptionsFactory.BuildSslServerOptions(context, serverCertificate);
                    await incomingSslStream.AuthenticateAsServerAsync(sslServerOptions, context.Token);
                    context.Logger.LogServerTLSHandshakeSucceeded();
                    context.IncomingStream?.TransferHandlersTo(incomingStream);
                }
                incomingReady.SetResult();
            }
            catch (ObjectDisposedException ex)
            {
                incomingReady.SetException(ex);
                throw;
                //LogRequestTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                //return;
            }
            catch (IOException ex)
            {
                incomingReady.SetException(ex);
                throw;
                //LogRequestTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                //return;
            }
            catch (AuthenticationException ex)
            {
                //incomingReady.SetException(ex);
                //throw;
                context.Logger.LogServerTLSHandshakeFailed(ex.InnerException?.Message ?? ex.Message);
                return;
            }
            catch (Exception ex)
            {
                //incomingReady.SetException(ex);
                //throw;
                LogRequestTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                return;
            }
            await Tunnel(context, incomingStream, outgoingStream, outgoingReady,
                context.ProxyCounters.IncomingReadCounter, context.ProxyCounters.OutgoingSendCounter,
                context.RequestTunnelState, context.ResponseTunnelState);
        }

        private static async Task ResponseTunnel(SessionContext context,
            SslStream? outgoingSslStream, CountingStream incomingStream, CountingStream outgoingStream,
            TaskCompletionSource incomingReady, TaskCompletionSource outgoingReady)
        {
            try
            {
                if (outgoingSslStream != null)
                {
                    var sslClientOptions = SslOptionsFactory.BuildSslClientOptions(context);
                    await outgoingSslStream.AuthenticateAsClientAsync(sslClientOptions, context.Token);
                    context.Logger.LogClientTLSHandshakeSucceeded(context.Host);
                    context.OutgoingStream?.TransferHandlersTo(outgoingStream);
                }
                outgoingReady.SetResult();
            }
            catch (ObjectDisposedException ex)
            {
                outgoingReady.SetException(ex);
                throw;
                //LogResponseTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                //return;
            }
            catch (IOException ex)
            {
                outgoingReady.SetException(ex);
                throw;
                //LogResponseTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                //return;
            }
            catch (AuthenticationException ex)
            {
                //incomingReady.SetException(ex);
                //throw;
                context.Logger.LogClientTLSHandshakeFailed(ex.InnerException?.Message ?? ex.Message, context.Host);
                return;
            }
            catch (Exception ex)
            {
                //outgoingReady.SetException(ex);
                //throw;
                LogResponseTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                return;
            }
            await Tunnel(context, outgoingStream, incomingStream, incomingReady,
                context.ProxyCounters.OutgoingReadCounter, context.ProxyCounters.IncomingSendCounter,
                context.ResponseTunnelState, context.RequestTunnelState);
        }

        private static async Task Tunnel(SessionContext context, 
            CountingStream source, CountingStream destination, TaskCompletionSource destinationReady,
            BytesReadCounter readCounter, BytesSendCounter sendCounter, TunnelState selfState,
            TunnelState otherTunnelState)
        {
            var buffer = new byte[BufferSize];
            using var ms = new MemoryStream();

            CancellationToken token = context.Token;

            bool reading = false;

            try
            {
                int bytesRead, headersEnd, searchStart = 0;
                bool readHeaders = selfState.Response ? context.ResponseHeader == null : context.RequestHeader == null;
                do
                {
                    if (readCounter.IsLogReading)
                    {
                        LogTunnelReading(context.Logger, selfState.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                    }
                    reading = true;
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    reading = false;
                    if (bytesRead > 0)
                    {
                        if (selfState.ResetReadHeaders)
                        {
                            selfState.ResetReadHeaders = false;
                            if (readCounter.IsLogReading)
                            {
                                LogTunnelResetReadingHeaders(context.Logger, selfState.TunnelId);
                            }
                            if (selfState.Response)
                            {
                                context.ResponseCacheEntry = null;
                                context.DisposeResponseCacheFileStream();
                            }
                            else
                            {
                                context.RequestHeader = null;
                                context.ResponseHeader = null;
                            }
                            readHeaders = selfState.Response ? context.ResponseHeader == null : context.RequestHeader == null;
                        }
                        if (!otherTunnelState.ResetReadHeaders)
                        {
                            if (readCounter.IsLogReading)
                            {
                                LogOtherTunnelResetReadingHeaders(context.Logger, 
                                    selfState.TunnelId, otherTunnelState.TunnelId);
                            }
                            otherTunnelState.ResetReadHeaders = true;
                        }
                        if (readHeaders)
                        {
                            if (readCounter.IsLogReading)
                            {
                                LogTunnelReadingHeaders(context.Logger,
                                    selfState.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                            }
                            ms.Write(buffer, 0, bytesRead);
                            if ((headersEnd = HttpParser.FindHeadersEnd(ms, selfState.Response, ref searchStart)) >= 0 || searchStart < 0)
                            {
                                readHeaders = false;
                                bool headerModified = false;
                                CacheEntry? requestCacheEntry = null;
                                var headerAndBody = HttpParser.GetHeaderLinesAndBody(ms, selfState.Response, headersEnd);
                                if (headerAndBody != null)
                                {
                                    if (readCounter.IsLogReading)
                                    {
                                        LogTunnelHeadersRead(context.Logger,
                                            selfState.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                                    }
                                    if (selfState.Response)
                                    {
                                        Debug.Assert(context.RequestHeader != null, "!!! HTTP Request Header is null !!!");
                                        context.ResponseHeader = new HttpResponseHeader(headerAndBody.HeaderLines, headersEnd + 4, context);
                                        if (!context.ResponseHeader.BadResponse)
                                        {
                                            if (CreateResponseCacheFileStream(context))
                                            {
                                                if (context.ResponseCacheFileStream != null)
                                                {
                                                    ms.Position = 0;
                                                    await ms.CopyToAsync(context.ResponseCacheFileStream);
                                                    await HandleEndOfResponseCacheFileStream(context);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Debug.Assert(context.ResponseHeader == null, "!!! HTTP Response Header is not null !!!");
                                        context.RequestHeader = new HttpRequestHeader(headerAndBody.HeaderLines, context);
                                        requestCacheEntry = await GetCacheEntry(context);
                                        if (context.Host.Overwritten)
                                        {
                                            if (headerAndBody.HeaderLines.Length > 0)
                                            {
                                                var modifiedFirstLine = HttpHeaderRewriter.OverrideHttpCommandHost(headerAndBody.HeaderLines[0], context.Host);
                                                if (modifiedFirstLine != null)
                                                {
                                                    headerAndBody.HeaderLines[0] = modifiedFirstLine;
                                                    headerModified = true;
                                                }
                                            }
                                            if (HttpHeaderRewriter.OverrideHostHeader(headerAndBody.HeaderLines, context.Host))
                                            {
                                                headerModified = true;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (readCounter.IsLogReading)
                                    {
                                        LogTunnelBodyRead(context.Logger,
                                            selfState.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                                    }
                                    if (context.ResponseCacheFileStream != null)
                                    {
                                        Debug.Assert(context.ResponseCacheEntry != null, "!!! Response cache entry is null !!!");
                                        if (context.ResponseCacheEntry != null)
                                        {
                                            await context.ResponseCacheFileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                                            await HandleEndOfResponseCacheFileStream(context);
                                        }
                                        else
                                        {
                                            context.DisposeResponseCacheFileStream();
                                        }
                                    }
                                }
                                if (requestCacheEntry != null)
                                {
                                    selfState.ResetReadHeaders = true;
                                    await ReplyCacheFile(requestCacheEntry, source, context);
                                }
                                else
                                {
                                    if (sendCounter.IsLogSending)
                                    {
                                        LogTunnelSending(context.Logger,
                                            selfState.TunnelId, StreamDirectionName.GetName(sendCounter.Direction));
                                    }
                                    await destinationReady.Task;
                                    if (headerAndBody != null && headerModified)
                                    {
                                        await SendModifiedHeadersAndBody(destination, headerAndBody.HeaderLines, headerAndBody.BodyBytes, token);
                                    }
                                    else
                                    {
                                        await destination.WriteAsync(ms.GetBuffer().AsMemory(0, (int)ms.Length), token);
                                    }
                                }
                                ms.SetLength(0);
                                ms.Position = 0;
                            }
                        }
                        else
                        {
                            if (readCounter.IsLogReading)
                            {
                                LogTunnelBodyRead(context.Logger,
                                    selfState.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                            }
                            if (context.ResponseCacheFileStream != null)
                            {
                                Debug.Assert(context.ResponseCacheEntry != null, "!!! Response cache entry is null !!!");
                                if (context.ResponseCacheEntry != null)
                                {
                                    await context.ResponseCacheFileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                                    await HandleEndOfResponseCacheFileStream(context);
                                }
                                else
                                {
                                    context.DisposeResponseCacheFileStream();
                                }
                            }
                            if (sendCounter.IsLogSending)
                            {
                                LogTunnelSending(context.Logger,
                                    selfState.TunnelId, StreamDirectionName.GetName(sendCounter.Direction));
                            }
                            await destinationReady.Task;
                            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        }
                    }
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException ex)
            {
                if (readCounter.IsLogReading)
                {
                    LogTunnelDebugError(context.Logger, ex.GetType().Name, ex.Message);
                }
            }
            catch (IOException ex)
            {
                if (readCounter.IsLogReading)
                {
                    LogTunnelDebugError(context.Logger, ex.GetType().Name, ex.Message);
                }
                if (reading)
                {
                    source.OnDisconnected();
                }
            }
            catch (Exception ex)
            {
                LogTunnelError(context.Logger, ex.GetType().Name, ex.Message, ex.StackTrace);
            }
        }

        private static async Task SendModifiedHeadersAndBody(Stream destination, string[] headerLines, byte[]? bodyBytes, CancellationToken token)
        {
            string modifiedHeaders = string.Join("\r\n", headerLines);
            byte[] modifiedHeaderBytes = Encoding.ASCII.GetBytes(modifiedHeaders);
            await destination.WriteAsync(modifiedHeaderBytes.AsMemory(0, modifiedHeaderBytes.Length), token);
            if (bodyBytes?.Length > 0)
            {
                await destination.WriteAsync(bodyBytes.AsMemory(0, bodyBytes.Length), token);
            }
        }

        private static async Task ReplyCacheFile(CacheEntry cacheEntry, CountingStream incomingStream, SessionContext context)
        {
            using var cacheFileStream = GetCacheEntryFileStream(cacheEntry);
            if (cacheFileStream != null)
            {
                context.ProxyCounters.SessionsCounter?.OnCacheResponse(context);
                await HttpProto.HttpReplyCacheFileStream(context, incomingStream, cacheEntry, cacheFileStream);
                context.Logger.LogResponseFromCache(cacheFileStream.Name);
            }
        }
    }
}
