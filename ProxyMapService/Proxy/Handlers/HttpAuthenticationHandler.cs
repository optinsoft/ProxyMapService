using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpAuthenticationHandler : IHandler
    {
        private static readonly HttpAuthenticationHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (!IsProxyAuthorizationHeaderPresent(context))
            {
                if (!IsAuthenticationRequired(context))
                {
                    context.SessionsCounter?.OnAuthenticationNotRequired(context);
                    return HandleStep.HttpAuthenticationNotRequired;
                }
                context.SessionsCounter?.OnAuthenticationRequired(context);
                await SendProxyAuthenticationRequired(context);
                return HandleStep.Terminate;
            }

            if (IsProxyAuthorizationCredentialsCorrect(context))
            {
                context.SessionsCounter?.OnAuthenticated(context);
                return HandleStep.HttpAuthenticated;
            }

            context.SessionsCounter?.OnAuthenticationInvalid(context);
            await SendProxyAuthenticationInvalid(context);
            return HandleStep.Terminate;
        }

        public static HttpAuthenticationHandler Instance()
        {
            return Self;
        }

        private static bool IsAuthenticationRequired(SessionContext context)
        {
            return context.ProxyAuthenticator.Required;
        }

        private static bool IsProxyAuthorizationHeaderPresent(SessionContext context)
        {
            return context.Http?.ProxyAuthorization != null;
        }

        private static bool IsProxyAuthorizationCredentialsCorrect(SessionContext context)
        {
            var proxyAuthorization = context.Http?.ProxyAuthorization;

            string[] parts = proxyAuthorization == null ? [] : Encoding.ASCII.GetString(Convert.FromBase64String(proxyAuthorization)).Split(':');

            var username = parts.Length > 0 ? parts[0] : null;
            var password = parts.Length > 1 ? parts[1] : null;

            return context.ProxyAuthenticator.Authenticate(username, password);
        }

        private static async Task SendProxyAuthenticationRequired(SessionContext context)
        {
            if (context.ClientStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"Pass Through Proxy\"\r\nConnection: close\r\n\r\n");
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }

        private static async Task SendProxyAuthenticationInvalid(SessionContext context)
        {
            if (context.ClientStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 407 Proxy Authentication Invalid\r\nProxy-Authenticate: Basic realm=\"Pass Through Proxy\"\r\nConnection: close\r\n\r\n");
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }
    }
}
