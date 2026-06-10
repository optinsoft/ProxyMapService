using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseProxyHandler : BaseResponseCacheHandler
    {
        protected static string? GetContextProxyUsernameWithParameters(SessionContext context)
        {
            return context.UsernameParameterResolver.GetUsernameWithParameters(context, context.ProxyServer?.Username, context.ProxyServer?.UsernameParameters);
        }

        protected static string? GetContextAuthenticationUsernameWithParameters(SessionContext context)
        {
            return context.UsernameParameterResolver.GetUsernameWithParameters(context, context.Mapping.Authentication.Username, context.Mapping.Authentication.UsernameParameters);
        }
    }
}
