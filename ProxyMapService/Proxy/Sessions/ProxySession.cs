using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Sessions
{
    public class ProxySession(string id, int lifetimeMinutes, ProxyServer proxyServer, long version)
    {
        public string Id { get; } = id;
        public DateTime ExpireTime { get; } = DateTime.Now.AddMinutes(lifetimeMinutes);
        public ProxyServer ProxyServer { get; } = proxyServer;        
        internal long Version = version;
        public bool IsExpired => DateTime.Now >= ExpireTime;
    }
}
