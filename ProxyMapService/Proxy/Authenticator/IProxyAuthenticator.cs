namespace ProxyMapService.Proxy.Authenticator
{
    public interface IProxyAuthenticator
    {
        bool Required { get; }
        bool Authenticate(string? username, string? password);
    }
}
