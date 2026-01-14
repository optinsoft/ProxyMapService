using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseAuthenticationHandler
    {
        private static void ResolveSessionId(SessionContext context)
        {
            context.UsernameParameterResolver.ResolveParameterValue(context.Mapping.Authentication.UsernameParameters.SessionId, context);
        }

        private static void ResolveSessionTime(SessionContext context)
        {
            context.UsernameParameterResolver.ResolveParameterValue(context.Mapping.Authentication.UsernameParameters.SessionTime, context);
        }

        private static void ResolveAuthenticationUserParameters(SessionContext context)
        {
            // Resolve SessionTime first (before SessionId)
            ResolveSessionTime(context);
            foreach (var p in context.Mapping.Authentication.UsernameParameters.Items)
            {
                if (!p.SessionTime) // Skip already resolved SessionTime
                {
                    context.UsernameParameterResolver.ResolveParameterValue(p, context);
                }
            }
        }

        private static void PopulateContext(SessionContext context)
        {
            context.SessionTime = context.Mapping.Listen.StickyProxyLifetime;
            if (context.Mapping.Authentication.SetAuthentication)
            {
                ResolveAuthenticationUserParameters(context);
            }
            else if (context.Mapping.Listen.StickyProxyLifetime > 0)
            {
                ResolveSessionTime(context);
                ResolveSessionId(context);
            }
            if (context.SessionId == null && context.SessionTime > 0)
            {
                context.SessionId = context.UsernameParameterResolver.GenerateSessionId(context, "^[A-Za-z]{8}");
            }
        }

        protected static void OnAuthenticationNotRequired(SessionContext context)
        {
            PopulateContext(context);
            context.SessionsCounter?.OnAuthenticationNotRequired(context);
        }

        protected static void OnAuthenticated(SessionContext context)
        {
            PopulateContext(context);
            context.SessionsCounter?.OnAuthenticated(context);
        }

        protected static void OnAuthenticationRequired(SessionContext context)
        {
            context.SessionsCounter?.OnAuthenticationRequired(context);
        }

        protected static void OnAuthenticationInvalid(SessionContext context)
        {
            context.SessionsCounter?.OnAuthenticationInvalid(context);
        }
    }
}
