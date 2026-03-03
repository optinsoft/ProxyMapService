using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpAuthenticationHandler : BaseAuthenticationHandler, IHandler
    {
        private static readonly HttpAuthenticationHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (!IsProxyAuthorizationHeaderPresent(context))
            {
                if (!IsAuthenticationRequired(context))
                {
                    OnAuthenticationNotRequired(context);
                    return HandleStep.HttpAuthenticationNotRequired;
                }
                OnAuthenticationRequired(context);
                await HttpProto.HttpReplyProxyAuthenticationRequired(context);
                return HandleStep.Terminate;
            }

            if (IsProxyAuthorizationCredentialsCorrect(context))
            {
                OnAuthenticated(context);
                return HandleStep.HttpAuthenticated;
            }

            OnAuthenticationInvalid(context);
            await HttpProto.HttpReplyProxyUnauthorized(context);
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

            return context.ProxyAuthenticator.Authenticate(context, username, password);
        }
    }
}
