namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyMapping(Listen listen)
    {
        public Listen Listen { get; init; } = listen;
        public Authentication Authentication { get; init; } = new();
        public List<ProxyServer> ProxyServers { get; init; } = [];

    }
}
