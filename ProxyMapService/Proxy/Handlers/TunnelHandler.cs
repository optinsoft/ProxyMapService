using Fare;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ssl;
using System;
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

        private class TunnelOptions
        {
            public bool ReadRequestHeaders;
            public bool ReadResponseHeaders;
            public bool OverrideHost;
        }

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && context.OutgoingStream != null && !context.Token.IsCancellationRequested)
            {
                using SslStream? incomingSslStream = context.Ssl ? new(context.IncomingStream) : null;
                using SslStream? outgoingSslStream = context.UpstreamSsl ? new(context.OutgoingStream) : null;

                string subjectName = $"CN=*.{context.Host.OriginalHostname}";
                X509Certificate2? serverCertificate = context.ServerCertificate;

                if (outgoingSslStream != null)
                {
                    await outgoingSslStream.AuthenticateAsClientAsync(SslOptionsFactory.BuildSslClientOptions(context), context.Token);
                    /*
                    if (outgoingSslStream.RemoteCertificate != null)
                    {
                        var cert = new X509Certificate2(outgoingSslStream.RemoteCertificate);
                        //context.Logger.LogDebug("Server Certificate Subject: {}", cert.Subject);
                        subjectName = cert.Subject;
                    }
                    */
                }

                if (incomingSslStream != null)
                {
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

                using CountingStream? incomingSslCountingStream =
                    incomingSslStream != null
                    ? new CountingStream(incomingSslStream, context,
                        context.ProxyCounters.IncomingReadSslCounter, context.ProxyCounters.IncomingSentSslCounter,
                        context.IncomingStream.ReadTunnelId, context.IncomingStream.SentTunnelId)
                    : null;
                using CountingStream? outgoingSslCountingStream =
                    outgoingSslStream != null
                    ? new CountingStream(outgoingSslStream, context,
                        context.ProxyCounters.OutgoingReadSslCounter, context.ProxyCounters.OutgoingSentSslCounter,
                        context.OutgoingStream.ReadTunnelId, context.OutgoingStream.SentTunnelId)
                    : null;

                await TwoWayTunnel(context,
                    incomingSslCountingStream ?? context.IncomingStream,
                    outgoingSslCountingStream ?? context.OutgoingStream);
            }

            return HandleStep.Terminate;
        }

        public static TunnelHandler Instance()
        {
            return Self;
        }

        private static async Task TwoWayTunnel(SessionContext context, Stream incomingStream, Stream outgoingStream)
        {
            var tunnelOptions = new TunnelOptions
            {
                OverrideHost = context.Host.Overwritten,
                ReadRequestHeaders = true, // context.Host.Overwritten,
                ReadResponseHeaders = true
            };
            var requestTunnelTask = Tunnel(incomingStream, outgoingStream, context,
                context.ProxyCounters.IncomingReadCounter, context.ProxyCounters.OutgoingSentCounter,
                context.RequestTunnelState, context.ResponseTunnelState, tunnelOptions);
            var responseTunnelTask = Tunnel(outgoingStream, incomingStream, context,
                context.ProxyCounters.OutgoingReadCounter, context.ProxyCounters.IncomingSentCounter,
                context.ResponseTunnelState, context.RequestTunnelState, tunnelOptions);
            await Task.WhenAny(requestTunnelTask, responseTunnelTask);
        }

        private static async Task Tunnel(Stream source, Stream destination, SessionContext context,
            BytesReadCounter readCounter, BytesSentCounter sentCounter, TunnelState state,
            TunnelState otherTunnelState, TunnelOptions options)
        {
            var buffer = new byte[BufferSize];
            using var ms = new MemoryStream();

            CancellationToken token = context.Token;

            try
            {
                int bytesRead, headersEnd, searchStart = 0;
                bool readHeaders = state.Response ? context.ResponseHeader == null : context.RequestHeader == null;
                do
                {
                    if (readCounter.IsLogReading)
                    {
                        context.Logger.LogDebug("Tunnel {tunnelId}: reading from {direction}...",
                            state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                    }
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    if (bytesRead > 0)
                    {
                        if (state.ResetReadHeaders)
                        {
                            state.ResetReadHeaders = false;
                            if (readCounter.IsLogReading)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: Reset reading headers", state.TunnelId);
                            }
                            if (state.Response)
                            {
                                context.ResponseCacheEntry = null;
                                context.DisposeResponseCacheFileStream();
                            }
                            else
                            {
                                context.RequestHeader = null;
                                context.ResponseHeader = null;
                            }
                            readHeaders = state.Response ? context.ResponseHeader == null : context.RequestHeader == null;
                        }
                        if (!otherTunnelState.ResetReadHeaders)
                        {
                            if (readCounter.IsLogReading)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: Resetting other tunnel ({otherTunnelId}) reading headers",
                                    state.TunnelId, otherTunnelState.TunnelId);
                            }
                            otherTunnelState.ResetReadHeaders = true;
                        }
                        if (readHeaders)
                        {
                            if (readCounter.IsLogReading)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: Reading headers from {direction}...",
                                    state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                            }
                            ms.Write(buffer, 0, bytesRead);
                            if ((headersEnd = HttpParser.FindHeadersEnd(ms, state.Response, ref searchStart)) >= 0 || searchStart < 0)
                            {
                                readHeaders = false;
                                bool headerModified = false;
                                CacheEntry? requestCacheEntry = null;
                                var headerAndBody = HttpParser.GetHeaderLinesAndBody(ms, state.Response, headersEnd);
                                if (headerAndBody != null)
                                {
                                    if (readCounter.IsLogReading)
                                    {
                                        context.Logger.LogDebug("Tunnel {tunnelId}: Headers read from {direction}",
                                            state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                                    }
                                    if (state.Response)
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
                                        if (options.OverrideHost)
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
                                            state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
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
                                    state.ResetReadHeaders = true;
                                    await ReplyCacheFile(requestCacheEntry, source, token);
                                }
                                else
                                {
                                    if (sentCounter.IsLogSending)
                                    {
                                        context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...",
                                            state.TunnelId, StreamDirectionName.GetName(sentCounter.Direction));
                                    }
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
                                    state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
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
                            if (sentCounter.IsLogSending)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...",
                                    state.TunnelId, StreamDirectionName.GetName(sentCounter.Direction));
                            }
                            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        }
                    }
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException)
            {
                //context.Logger.LogError("ObjectDisposedException: {ErrorMessage}", ex.Message);
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

        private static async Task ReplyCacheFile(CacheEntry cacheEntry, Stream incomingStream, CancellationToken token)
        {
            using var cacheFileStream = GetCacheEntryFileStream(cacheEntry);
            if (cacheFileStream != null)
            {
                await HttpProto.HttpReplyCacheFileStream(incomingStream, token, cacheFileStream);
            }
        }
    }
}
