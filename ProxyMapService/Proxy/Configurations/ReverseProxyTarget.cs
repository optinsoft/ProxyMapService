namespace ProxyMapService.Proxy.Configurations
{
    public class ReverseProxyTarget
    {
        public string Host { get; set; } = String.Empty;

        /// <summary>
        /// Upstream port. 0 — derive from the upstream connection scheme:
        /// 443 when the connection to the target is TLS, 80 otherwise.
        /// </summary>
        public int Port { get; set; } = 0;
    }
}
