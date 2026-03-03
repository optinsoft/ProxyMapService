using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseProxyHandler
    {
        private static string? GetUsernameWithParameters(SessionContext context, string? username, UsernameParameterList? parameterList)
        {
            if (username == null) return null;
            if (parameterList != null)
            {
                foreach (var p in parameterList)
                {
                    string? value = context.UsernameParameterResolver.ResolveParameterValue(p, context);
                    if (!String.IsNullOrEmpty(value))
                    {
                        if (p.Name != "account")
                        {
                            username += $"-{p.Name}-{value}";
                        }
                    }
                }
            }
            return username;
        }

        protected static string? GetContextProxyUsernameWithParameters(SessionContext context)
        {
            return GetUsernameWithParameters(context, context.ProxyServer?.Username, context.ProxyServer?.UsernameParameters);
        }

        protected static string? GetContextAuthenticationUsernameWithParameters(SessionContext context)
        {
            return GetUsernameWithParameters(context, context.Mapping.Authentication.Username, context.Mapping.Authentication.UsernameParameters);
        }
    }
}
