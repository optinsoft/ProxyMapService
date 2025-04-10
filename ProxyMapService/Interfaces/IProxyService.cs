using ProxyMapService.Models;
using ProxyMapService.Proxy.Counters;

namespace ProxyMapService.Interfaces
{
    public interface IProxyService
    {
        string GetServiceInfo();
        string GetCurrentTime();
        int GetSessionsCount();
        int GetAuthenticationNotRequired();
        int GetAuthenticationRequired();
        int GetAuthenticated();
        int GetAuthenticationInvalid();
        int GetHttpRejected();
        int GetProxyConnected();
        int GetProxyFailed();
        int GetBypassConnected();
        int GetBypassFailed();
        int GetHeaderFailed();
        int GetNoHost();
        int GetHostRejected();
        int GetHostProxified();
        int GetHostBypassed();
        long GetTotalBytesRead();
        long GetTotalBytesSent();
        long GetProxyBytesRead();
        long GetProxyBytesSent();
        long GetBypassBytesRead();
        long GetBypassBytesSent();
        IEnumerable<KeyValuePair<string, HostStats>>? GetHostStats();
    }
}
