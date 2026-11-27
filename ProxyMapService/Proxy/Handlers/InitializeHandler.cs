using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Handlers
{
    public class InitializeHandler : IHandler
    {
        private static readonly InitializeHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            try
            {
                context.CreateIncomingClientStream();
                if (context.IncomingStream != null)
                {
                    context.IncomingStream.DisconnectHandler += HandlerLogger.OnClientDisconnected;
                }

                if (context.Mapping.Listen.Mode == ModeEnum.Reverse)
                {
                    return await RunReverse(context);
                }

                var requestHeaderBytes = await context.IncomingHeaderStream.ReadHeaderBytes(context.IncomingStream, context.Token);
                switch (context.IncomingHeaderStream.ProtocolType)
                {
                    case ProtocolTypeEnum.Http:
                        if (requestHeaderBytes == null)
                        {
                            context.Logger.LogReadHeaderFailed("HTTP");
                            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                            return HandleStep.Terminate;
                        }
                        context.InboundType = ProxyType.Http;
                        context.Http = new HttpRequestHeader(requestHeaderBytes);
                        if (context.Http.HTTPTargetHost != null)
                        {
                            //initialize context.Host before logging HttpRequestHeader
                            context.Host = context.Http.HTTPTargetHost;
                        }
                        // do not fall back to the Host header when determining the connection target.                        
                        context.RequestHeadersLogger?.OnHttpRequestHeader(context, context.Http);
                        context.SessionManager.UpdateClientUserAgent(context, context.Http);                        
                        if (context.Http.BadRequest)
                        {
                            context.Logger.LogHttpBadRequest();
                            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                            await HttpProto.HttpReplyBadRequest(context, context.Http);
                            return HandleStep.Terminate;
                        }
                        return HandleStep.HttpInitialized;
                    case ProtocolTypeEnum.Socks4:
                        if (requestHeaderBytes == null)
                        {
                            context.Logger.LogReadHeaderFailed("SOCKS4");
                            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                            return HandleStep.Terminate;
                        }
                        context.InboundType = ProxyType.Socks4;
                        context.Socks4 = new Socks4Header(requestHeaderBytes);
                        if (context.Socks4.IsConnectRequest(requestHeaderBytes))
                        {
                            return HandleStep.Socks4Initialized;
                        }
                        context.Logger.LogSocks4BadRequest(context.Socks4.CommandCode);
                        break;
                    case ProtocolTypeEnum.Socks5:
                        if (requestHeaderBytes == null)
                        {
                            context.Logger.LogReadHeaderFailed("SOCKS5");
                            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                            return HandleStep.Terminate;
                        }
                        context.InboundType = ProxyType.Socks5;
                        context.Socks5 = new Socks5Header(requestHeaderBytes);
                        return HandleStep.Socks5Initialized;
                    default:
                        context.Logger.LogUnknownProtocol();
                        break;
                }
            }
            catch (Exception)
            {
                context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                throw;
            }
            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
            return HandleStep.Terminate;
        }

        /// <summary>
        /// Reverse proxy mode. The client protocol is the target server's protocol, not a proxy
        /// protocol, so it is detected without consuming bytes whenever possible:
        /// a TLS connection (peeked, not read — TunnelHandler reuses the cached peek record for
        /// its own detection, MITM and passthrough) and any opaque traffic are tunneled as is;
        /// only an HTTP request is actually read and parsed, because the HTTP handlers re-send
        /// the (rewritten) request themselves before tunneling.
        /// </summary>
        private async Task<HandleStep> RunReverse(SessionContext context)
        {
            try
            {
                // SslMode.Yes — trust the configuration, no peek needed.
                // SslMode.No — TLS is excluded, go straight to the HTTP branch.
                // SslMode.Auto — peek the TLS record (peek does not consume bytes).
                bool isTls = context.SslMode switch
                {
                    SslMode.Yes => true,
                    SslMode.No => false,
                    _ => await context.IncomingStream!.PeekTlsRecordAsync(context.Token) != null
                };
                if (isTls)
                {
                    return HandleStep.ReverseProxy;
                }

                // Not TLS: commit to the HTTP path and parse the request header.
                // The protocol type is preset so that ReadHeaderBytes does not mistake
                // SOCKS/proxy traffic for something else — in reverse mode everything
                // that does not parse as HTTP is simply terminated.
                context.IncomingHeaderStream.ProtocolType = ProtocolTypeEnum.Http;
                var requestHeaderBytes = await context.IncomingHeaderStream.ReadHeaderBytes(context.IncomingStream, context.Token);
                if (requestHeaderBytes == null)
                {
                    context.Logger.LogReadHeaderFailed("HTTP (reverse)");
                    context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                    return HandleStep.Terminate;
                }
                context.InboundType = ProxyType.Http;
                context.Http = new HttpRequestHeader(requestHeaderBytes);
                // context.Host is intentionally NOT taken from the request:
                // in reverse mode it comes from ReverseProxy.Target (ReverseProxyActionHandler).
                context.RequestHeadersLogger?.OnHttpRequestHeader(context, context.Http);
                context.SessionManager.UpdateClientUserAgent(context, context.Http);
                if (context.Http.BadRequest)
                {
                    context.Logger.LogHttpBadRequest();
                    context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                    await HttpProto.HttpReplyBadRequest(context, context.Http);
                    return HandleStep.Terminate;
                }
                return HandleStep.ReverseProxyHttp;
            }
            catch (Exception)
            {
                context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                throw;
            }
        }

        public static InitializeHandler Instance()
        {
            return Self;
        }
    }
}
