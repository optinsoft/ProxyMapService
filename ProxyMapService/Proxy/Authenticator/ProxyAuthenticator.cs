using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;
using System.Collections.Specialized;

namespace ProxyMapService.Proxy.Authenticator
{
    public class ProxyAuthenticator(Authentication authentication) : IProxyAuthenticator
    {
        private readonly Authentication _authentication = authentication;

        public bool Required { get => _authentication.Required; }

        public bool Authenticate(SessionContext context, string? username, string? password)
        {
            context.Username = username;
            var account = username;
            if (_authentication.ParseUsernameParameters && username != null)
            {
                context.UsernameParameters = ParseUsername(username);
                account = context.UsernameParameters.Items[0].Value;
            }
            if (!_authentication.Verify)
            {
                return true;
            }
            if (account == null || password == null) 
            { 
                return false; 
            }
            return account == _authentication.Username && password == _authentication.Password;
        }

        private static UsernameParameterList ParseUsername(string username) 
        {
            var parts = username.Split('-');
            UsernameParameterList uparams = new();
            uparams.SetValue("account", parts[0]);
            for (int i = 1; i < parts.Length - 1; i += 2)
            {
                var paramName = parts[i];
                var paramValue = parts[i + 1];
                uparams.SetValue(paramName, paramValue);
            }
            return uparams;
        }
    }
}
