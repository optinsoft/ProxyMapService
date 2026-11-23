using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    /// <summary>
    /// Reverse proxy mode (Listen.Mode = Reverse). The target host is fixed in the
    /// configuration (ReverseProxy.Target), not taken from the client's request, so
    /// HostRules and client proxy protocols (SOCKS, forward HTTP proxying) do not apply.
    ///
    /// Handles both entry steps produced by InitializeHandler:
    ///   HandleStep.ReverseProxy — TLS or opaque traffic (nothing was consumed from the
    ///   incoming stream): connect to the target and hand the session over to TunnelHandler
    ///   (TLS detection, MITM, TlsRecordSplit keep working because no byte was read);
    ///   HandleStep.ReverseProxyHttp — a parsed HTTP request: dispatch to the regular
    ///   HTTP handlers (Bypass/Proxy/File), reusing their request rewriting and replies.
    ///
    /// How the connection to the target is established (directly or through the
    /// ProxyServers chain) is defined by Listen.Action, as in the forward mode
    /// (Allow acts as a synonym of Proxy).
    /// </summary>
    public class ReverseProxyActionHandler : IHandler
    {
        private static readonly ReverseProxyActionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            var reverseProxy = context.Mapping.ReverseProxy;

            if (context.Host.Hostname.Length == 0 && reverseProxy != null && !string.IsNullOrEmpty(reverseProxy.Target.Host))
            {
                // Target.Port == 0 (not set): the default port is derived from the upstream
                // connection scheme — 443 for TLS upstream, 80 for plaintext.
                var port = reverseProxy.Target.Port != 0
                    ? reverseProxy.Target.Port
                    : (await IsUpstreamTls(context) ? 443 : 80);
                context.Host = new HostAddress(reverseProxy.Target.Host, port);
            }
            context.UpstreamServerName = reverseProxy?.ServerName;

            context.HostAction = context.Action;
            context.SessionManager.PopulateContext(context);

            switch (context.Action.ActionValue)
            {
                case SessionActionEnum.Bypass:
                    if (reverseProxy == null || string.IsNullOrEmpty(reverseProxy.Target.Host))
                    {
                        context.Logger.LogReverseProxyNotConfigured(context.InboundEndpoint.Port);
                        return HandleStep.Terminate;
                    }
                    // HttpBypassHandler connects directly; with context.Http == null it only
                    // opens the connection and hands the session over to TunnelHandler.
                    return HandleStep.HttpBypass;

                case SessionActionEnum.Proxy:
                    if (reverseProxy == null || string.IsNullOrEmpty(reverseProxy.Target.Host))
                    {
                        context.Logger.LogReverseProxyNotConfigured(context.InboundEndpoint.Port);
                        return HandleStep.Terminate;
                    }
                    // ProxyHandler connects to the proxy server; the protocol-specific handlers
                    // build a CONNECT to context.Host when the client protocol is unknown
                    // (context.Http/Socks4/Socks5 == null), which is exactly the reverse case.
                    return HandleStep.Proxy;

                case SessionActionEnum.File:
                    if (context.Http != null)
                    {
                        return HandleStep.HttpFile;
                    }
                    // TLS/opaque traffic cannot be answered with files.
                    context.Logger.LogReverseProxyFileRequiresHttp(context.InboundEndpoint.Port);
                    return HandleStep.Terminate;

                default: // Deny
                    context.Logger.LogHostRejected(context.Host.Hostname, context.Host.Port);
                    context.ProxyCounters.SessionsCounter?.OnHostRejected(context);
                    if (context.Http != null)
                    {
                        await HttpProto.HttpReplyForbidden(context, context.Http);
                    }
                    return HandleStep.Terminate;
            }
        }

        public static ReverseProxyActionHandler Instance()
        {
            return Self;
        }

        /// <summary>
        /// Whether the connection to the reverse target should be TLS. Used to pick the
        /// default upstream port (443/80) when ReverseProxy:Target:Port is not set.
        /// Mirrors the TunnelHandler heuristics: UpstreamSslMode decides explicitly,
        /// Auto falls back to the incoming side TLS-ness.
        /// </summary>
        private static async Task<bool> IsUpstreamTls(SessionContext context)
        {
            switch (context.UpstreamSslMode)
            {
                case SslMode.Yes:
                    return true;
                case SslMode.No:
                    return false;
            }

            // Auto: follow the incoming side. For SslMode.Yes the answer is already known;
            // for SslMode.No the session is on the HTTP path. For Auto the TLS record was
            // peeked in InitializeHandler (the peek is cached, so this call is instant).
            if (context.SslMode == SslMode.Yes)
            {
                return true;
            }
            if (context.SslMode == SslMode.No)
            {
                return false;
            }
            return context.IncomingStream != null &&
                   await context.IncomingStream.PeekTlsRecordAsync(context.Token) != null;
        }
    }
}
