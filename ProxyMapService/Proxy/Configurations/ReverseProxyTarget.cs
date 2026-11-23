namespace ProxyMapService.Proxy.Configurations
{
    public class ReverseProxyTarget
    {
        public string Host { get; set; } = String.Empty;

        /// <summary>
        /// Upstream port. 0 — mirror the local listening port (useful together with
        /// Listen.PortRange: a connection to local port N is forwarded to Target.Host:N).
        /// </summary>
        public int Port { get; set; } = 0;
    }
}
