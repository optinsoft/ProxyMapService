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
        int GetConnected();
        int GetConnectionFailed();
        long GetTotalBytesRead();
        long GetTotalBytesSent();
    }
}
