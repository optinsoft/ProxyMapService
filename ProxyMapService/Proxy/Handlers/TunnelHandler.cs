using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ssl;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class TunnelHandler : BaseResponseCacheHandler, IHandler
    {
        private static readonly TunnelHandler Self = new();
        private const int BufferSize = 8192;

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && context.OutgoingStream != null && !context.Token.IsCancellationRequested)
            {
                using SslStream? incomingSslStream = context.Ssl ? new(context.IncomingStream) : null;
                using SslStream? outgoingSslStream = context.UpstreamSsl ? new(context.OutgoingStream) : null;

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
                    await incomingSslStream.AuthenticateAsServerAsync(SslOptionsFactory.BuildSslServerOptions(context, serverCertificate), context.Token);
                }
                incomingReady.SetResult();
            }
            catch (ObjectDisposedException ex)
            {
                incomingReady.SetException(ex);
                //context.Logger.LogError("[RequestTunnel] {ExceptionName}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                throw;
            }
            catch (IOException ex)
            {
                incomingReady.SetException(ex);
                //context.Logger.LogError("[RequestTunnel] {ExceptionName}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                incomingReady.SetException(ex);
                context.Logger.LogError("[RequestTunnel] {ExceptionName}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                throw;
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
                    await outgoingSslStream.AuthenticateAsClientAsync(SslOptionsFactory.BuildSslClientOptions(context), context.Token);
                }
                outgoingReady.SetResult();
            }
            catch (ObjectDisposedException ex)
            {
                outgoingReady.SetException(ex);
                //context.Logger.LogError("[ResponseTunnel] {ExceptionName}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                throw;
            }
            catch (IOException ex)
            {
                outgoingReady.SetException(ex);
                //context.Logger.LogError("[ResponseTunnel] {ExceptionName}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                outgoingReady.SetException(ex);
                context.Logger.LogError("[ResponseTunnel] {ExceptionName}: {ErrorMessage}", ex.GetType().Name, ex.Message);
                throw;
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

            try
            {
                int bytesRead, headersEnd, searchStart = 0;
                bool readHeaders = selfState.Response ? context.ResponseHeader == null : context.RequestHeader == null;
                do
                {
                    if (readCounter.IsLogReading)
                    {
                        context.Logger.LogDebug("Tunnel {tunnelId}: reading from {direction}...",
                            selfState.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                    }
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    if (bytesRead > 0)
                    {
                        if (selfState.ResetReadHeaders)
                        {
                            selfState.ResetReadHeaders = false;
                            if (readCounter.IsLogReading)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: Reset reading headers", selfState.TunnelId);
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
                                context.Logger.LogDebug("Tunnel {tunnelId}: Resetting other tunnel ({otherTunnelId}) reading headers",
                                    selfState.TunnelId, otherTunnelState.TunnelId);
                            }
                            otherTunnelState.ResetReadHeaders = true;
                        }
                        if (readHeaders)
                        {
                            if (readCounter.IsLogReading)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: Reading headers from {direction}...",
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
                                        context.Logger.LogDebug("Tunnel {tunnelId}: Headers read from {direction}",
                                            selfState.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                                    }
                                    if (selfState.Response)
                                    {
                                        Debug.Assert(context.RequestHeader != null, "!!! HTTP Request Header is null !!!");
                                        context.ResponseHeader = new HttpResponseHeader(headerAndBody.HeaderLines, headersEnd + 4);
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
                                    else
                                    {
                                        Debug.Assert(context.ResponseHeader == null, "!!! HTTP Response Header is not null !!!");
                                        context.RequestHeader = new HttpRequestHeader(headerAndBody.HeaderLines);
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
                                        context.Logger.LogDebug("Tunnel {tunnelId}: Body read from {direction}",
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
                                        context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...",
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
                                context.Logger.LogDebug("Tunnel {tunnelId}: Body read from {direction}",
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
                                context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...",
                                    selfState.TunnelId, StreamDirectionName.GetName(sendCounter.Direction));
                            }
                            await destinationReady.Task;
                            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        }
                    }
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException)
            {
                //context.Logger.LogError("[Tunnel] {ExceptionName}: {ErrorMessage}", ex.GetType().Name, ex.Message);
            }
            catch (IOException)
            {
                //context.Logger.LogError("[Tunnel] {ExceptionName}: {ErrorMessage}", ex.GetType().Name, ex.Message);
            }
            catch (Exception ex)
            {
                context.Logger.LogError("[Tunnel] {ExceptionName}: {ErrorMessage}", ex.GetType().Name, ex.Message);
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
                await HttpProto.HttpReplyCacheFileStream(context, incomingStream, cacheFileStream);
            }
        }
    }
}
