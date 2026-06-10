using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseAuthenticationHandler
    {
        protected static void OnAuthenticationNotRequired(SessionContext context)
        {
            //context.UsernameParameterResolver.PopulateContext(context);
            context.ProxyCounters.SessionsCounter?.OnAuthenticationNotRequired(context);
        }

        protected static void OnAuthenticated(SessionContext context)
        {
            //context.UsernameParameterResolver.PopulateContext(context);
            context.ProxyCounters.SessionsCounter?.OnAuthenticated(context);
        }

        protected static void OnAuthenticationRequired(SessionContext context)
        {
            context.ProxyCounters.SessionsCounter?.OnAuthenticationRequired(context);
        }

        protected static void OnAuthenticationInvalid(SessionContext context)
        {
            context.ProxyCounters.SessionsCounter?.OnAuthenticationInvalid(context);
        }
    }
}
