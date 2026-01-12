using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Providers
{
    public interface IProxyProvider
    {
        ProxyServer GetProxyServer();
    }
}
