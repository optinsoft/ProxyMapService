using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Provider
{
    public interface IProxyProvider
    {
        ProxyServer GetProxyServer();
    }
}
