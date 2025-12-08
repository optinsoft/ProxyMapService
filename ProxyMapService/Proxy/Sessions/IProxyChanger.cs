using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Sessions
{
    public interface IProxyChanger
    {
        ProxyServer GetProxyServer();
    }
}
