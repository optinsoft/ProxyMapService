namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyMapping(Listen listen, Authentication authentication)
    {
        public Listen Listen { get; private set; } = listen;
        public Authentication Authentication { get; private set; } = authentication;
        public List<ProxyServer> ProxyServers { get; private set; } = [];

    }
}
