using ProxyMapService.Proxy.Network;

namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyServer(string host, int port, ProxyType proxyType = ProxyType.Http)
    {
        public string Host { get; private set; } = host;
        public int Port { get; private set; } = port;
        public ProxyType ProxyType { get; private set; } = proxyType;
    }
}
