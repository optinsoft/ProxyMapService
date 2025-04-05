using ProxyMapService.Proxy.Counters;

namespace ProxyMapService.Interfaces
{
    public interface IProxyService
    {
        string GetServiceInfo();
        int GetSessionsCount();
        int GetAuthenticationNotRequired();
        int GetAuthenticationRequired();
        int GetAuthenticated();
        int GetAuthenticationInvalid();
        int GetHttpRejected();
        long GetTotalBytesRead();
        long GetTotalBytesSent();
    }
}
