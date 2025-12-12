using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Authenticator
{
    public interface IProxyAuthenticator
    {
        bool Required { get; }
        bool Authenticate(SessionContext context, string? username, string? password);
    }
}
