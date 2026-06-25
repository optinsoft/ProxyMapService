using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Utils
{
    public static class ProxyHandlerUtils
    {
        public static string? GetContextProxyUsernameWithParameters(SessionContext context)
        {
            return context.UsernameParameterResolver.GetUsernameWithParameters(context, context.ProxyServer?.Username, context.ProxyServer?.UsernameParameters);
        }

        public static string? GetContextAuthenticationUsernameWithParameters(SessionContext context)
        {
            return context.UsernameParameterResolver.GetUsernameWithParameters(context, context.Mapping.Authentication.Username, context.Mapping.Authentication.UsernameParameters);
        }
    }
}
