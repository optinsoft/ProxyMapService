using ProxyMapService.Models;
using ProxyMapService.Proxy.Counters;

namespace ProxyMapService.Interfaces
{
    public interface IProxyService
    {
        CancellationToken StoppingToken { get; set; }
        bool Started { get; }
        void ResetStats();
        void StartProxyMappingTasks();
        void StopProxyMappingTasks();
        string GetServiceInfo();
        string? GetStartTime();
        string? GetStopTime();
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
