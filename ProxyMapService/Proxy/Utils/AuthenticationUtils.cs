using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Utils
{
    public static class AuthenticationUtils
    {
        public static void OnAuthenticationNotRequired(SessionContext context)
        {
            //context.UsernameParameterResolver.PopulateContext(context);
            context.ProxyCounters.SessionsCounter?.OnAuthenticationNotRequired(context);
        }

        public static void OnAuthenticated(SessionContext context)
        {
            //context.UsernameParameterResolver.PopulateContext(context);
            context.ProxyCounters.SessionsCounter?.OnAuthenticated(context);
        }

        public static void OnAuthenticationRequired(SessionContext context)
        {
            context.ProxyCounters.SessionsCounter?.OnAuthenticationRequired(context);
        }

        public static void OnAuthenticationInvalid(SessionContext context)
        {
            context.ProxyCounters.SessionsCounter?.OnAuthenticationInvalid(context);
        }
    }
}
