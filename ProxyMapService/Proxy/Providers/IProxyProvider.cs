using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Providers
{
    public interface IProxyProvider
    {
        void CleanupExpiredSessions();
        ProxyServer GetProxyServer(SessionContext context);
    }
}
