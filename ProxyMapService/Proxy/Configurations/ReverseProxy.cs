namespace ProxyMapService.Proxy.Configurations
{
    /// <summary>
    /// Reverse proxy settings (used when Listen.Mode = Reverse).
    /// How the connection to the target is established (directly or through the
    /// ProxyServers chain) is defined by Listen.Action, as in the forward mode.
    /// </summary>
    public class ReverseProxy
    {
        public ReverseProxyTarget Target { get; set; } = new();

        /// <summary>
        /// SNI for the upstream TLS connection. Null — Target.Host is used.
        /// Affects only the TLS ClientHello (SNI/TargetHost), not the connect endpoint.
        /// </summary>
        public string? ServerName { get; set; } = null;
    }
}
