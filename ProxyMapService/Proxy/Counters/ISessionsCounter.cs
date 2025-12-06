using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public interface ISessionsCounter
    {
        int Count { get; }
        int AuthenticationNotRequired { get; }
        int AuthenticationRequired { get; }
        int Authenticated { get; }
        int AuthenticationInvalid { get; }
        int HttpRejected { get; }
        int ProxyConnected { get; }
        int ProxyFailed { get; }
        int BypassConnected { get; }
        int BypassFailed { get; }
        int HeaderFailed { get; }
        int NoHost { get; }
        int HostRejected { get; }
        int HostProxified { get; }
        int HostBypassed { get; }
        int Socks5Failures { get; }
        void Reset();
        void OnSessionStarted(SessionContext context);
        void OnAuthenticationNotRequired(SessionContext context);
        void OnAuthenticationRequired(SessionContext context);
        void OnAuthenticated(SessionContext context);
        void OnAuthenticationInvalid(SessionContext context);
        void OnHttpRejected(SessionContext context);
        void OnProxyConnected(SessionContext context);
        void OnProxyFailed(SessionContext context);
        void OnBypassConnected(SessionContext context);
        void OnBypassFailed(SessionContext context);
        void OnHeaderFailed(SessionContext context);
        void OnNoHost(SessionContext context);
        void OnHostRejected(SessionContext context);
        void OnHostProxified(SessionContext context);
        void OnHostBypassed(SessionContext context);
        void OnSocks5Failure(SessionContext context);
    }
}
