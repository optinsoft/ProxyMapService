namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyMapping(Listen listen, Authentication authentication, ProxyServer proxyServer)
    {
        public Listen Listen { get; private set; } = listen;
        public Authentication Authentication { get; private set; } = authentication;
        public ProxyServer ProxyServer { get; private set; } = proxyServer;
    }
}
