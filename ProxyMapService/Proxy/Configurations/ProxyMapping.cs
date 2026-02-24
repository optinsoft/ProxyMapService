namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyMapping
    {
        public Listen Listen { get; init; } = new();
        public Authentication Authentication { get; init; } = new();
        public ProxyServers ProxyServers { get; init; } = new();
    }
}
