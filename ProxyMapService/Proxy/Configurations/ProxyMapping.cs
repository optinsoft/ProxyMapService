namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyMapping
    {
        public Listen Listen { get; init; } = new();
        public Authentication Authentication { get; init; } = new();
        public ProxyServers ProxyServers { get; init; } = new();
        public CacheRules? CacheRules { get; init; } = null;
        /// <summary>
        /// Root directory for Action = File. In the forward mode it can be overridden
        /// per host by HostRule.RootDir; in the reverse mode it is the only source.
        /// </summary>
        public string? RootDir { get; init; } = null;
        /// <summary>
        /// Reverse proxy settings. Used only when Listen.Mode = Reverse.
        /// </summary>
        public ReverseProxy? ReverseProxy { get; init; } = null;
    }
}
