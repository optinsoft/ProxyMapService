using Proxy.Headers;
using ProxyMapService.Proxy.Sessions;
using System.Reflection.PortableExecutable;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class AuthenticationHandler : IHandler
    {
        private static readonly AuthenticationHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (!IsProxyAuthorizationHeaderPresent(context))
            {
                if (!IsAuthenticationRequired(context))
                {
                    return HandleStep.AuthenticationNotRequired;
                }
                await SendProxyAuthenticationRequired(context);
                return HandleStep.Terminate;
            }

            if (!IsVerifyAuthentication(context))
            {
                return HandleStep.Authenticated;
            }

            if (IsProxyAuthorizationCredentialsCorrect(context))
            {
                return HandleStep.Authenticated;
            }

            await SendProxyAuthenticationInvalid(context);
            return HandleStep.Terminate;
        }

        public static AuthenticationHandler Instance()
        {
            return Self;
        }

        private static bool IsAuthenticationRequired(SessionContext context)
        {
            return context.mapping.Authentication.Required;
        }

        private static bool IsProxyAuthorizationHeaderPresent(SessionContext context)
        {
            return context.Header?.ProxyAuthorization != null;
        }

        private static bool IsVerifyAuthentication(SessionContext context)
        {
            return context.mapping.Authentication.Verify;
        }

        private static bool IsProxyAuthorizationCredentialsCorrect(SessionContext context)
        {
            var proxyAuthorization = context.Header?.ProxyAuthorization;

            if (proxyAuthorization == null) return false;

            return Encoding.ASCII.GetString(Convert.FromBase64String(proxyAuthorization)) == $"{context.mapping.Authentication.Username}:{context.mapping.Authentication.Password}";
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
