using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Authenticator
{
    public class ProxyAuthenticator(Authentication authentication) : IProxyAuthenticator
    {
        private readonly Authentication _authentication = authentication;

        public bool Required { get => _authentication.Required; }

        public bool Authenticate(string? username, string? password)
        {
            if (!_authentication.Verify)
            {
                return true;
            }
            if (username == null || password == null) 
            { 
                return false; 
            }
            return username == _authentication.Username && password == _authentication.Password;
        }
    }
}
