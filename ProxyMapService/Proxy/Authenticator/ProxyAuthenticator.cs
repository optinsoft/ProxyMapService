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
            context.Username = username;
            if (authentication.ParseUsernameParameters && username != null)
            {
                context.UsernameParameters = ParseUsername(username);
                context.Username = context.UsernameParameters.Items[0].Value;
                foreach (var p in authentication.UsernameParameters.Items)
                {
                    string? value = p.Value;
                    string? contextParamValue = null;
                    if (value.StartsWith('$'))
                    {
                        var contextParamName = value.Substring(1);
                        contextParamValue = context.UsernameParameters.GetValue(contextParamName);
                        value = contextParamValue ?? p.Default;
                    }
                    if (contextParamValue == null)
                    {
                        if (value != null && value.StartsWith('^'))
                        {
                            var pattern = value.Substring(1);
                            var xeger = new Xeger(pattern);
                            value = xeger.Generate();
                        }
                    }
                    if (!String.IsNullOrEmpty(value))
                    {
                        context.UsernameParameters.SetValue(p.Name, value);
                    }
                }
            }
            if (!authentication.Verify)
            {
                return true;
            }
            if (context.Username == null || password == null) 
            { 
                return false; 
            }
            return context.Username == authentication.Username && password == authentication.Password;
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
