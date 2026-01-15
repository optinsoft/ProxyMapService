using Fare;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;
using System.Collections.Specialized;

namespace ProxyMapService.Proxy.Authenticator
{
    public class ProxyAuthenticator(Authentication authentication) : IProxyAuthenticator
    {
        public bool Required { get => authentication.Required; }

        public bool Authenticate(SessionContext context, string? username, string? password)
        {
            var account = username;
            if (authentication.ParseUsernameParameters && username != null)
            {
                context.UsernameParameters = ParseUsername(username);
                account = context.UsernameParameters[0].Value;
            }
            if (!authentication.Verify)
            {
                return true;
            }
            if (account == null || password == null) 
            { 
                return false; 
            }
            return account == authentication.Username && password == authentication.Password;
        }

        private UsernameParameterList ParseUsername(string username) 
        {
            var parts = username.Split('-');
            UsernameParameterList uparams = new();
            uparams.SetValue("account", parts[0]);
            for (int i = 1; i < parts.Length - 1; i += 2)
            {
                var paramName = parts[i];
                var paramValue = parts[i + 1];
                var authParam = authentication.UsernameParameters.FindParameter(paramName);
                uparams.SetValue(paramName, paramValue, authParam);
            }
            return uparams;
        }
    }
}
