using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4AuthenticationHandler : BaseAuthenticationHandler, IHandler
    {
        private static readonly Socks4AuthenticationHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (!IsUserIdPresent(context))
            {
                if (!IsAuthenticationRequired(context))
                {
                    OnAuthenticationNotRequired(context);
                    return HandleStep.Socks4AuthenticationNotRequired;
                }
                context.Logger.LogSocks4ProxyAuthenticationRequired();
                OnAuthenticationRequired(context);
                await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed);
                return HandleStep.Terminate;
            }

            if (IsProxyAuthorizationCredentialsCorrect(context))
            {
                OnAuthenticated(context);
                return HandleStep.Socks4Authenticated;
            }

            context.Logger.LogSocks4ProxyIncorrectCredentials();
            OnAuthenticationInvalid(context);
            await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed);
            return HandleStep.Terminate;
        }

        public static Socks4AuthenticationHandler Instance()
        {
            return Self;
        }

        private static bool IsAuthenticationRequired(SessionContext context)
        {
            return context.ProxyAuthenticator.Required;
        }

        private static bool IsUserIdPresent(SessionContext context)
        {
            return context.Socks4?.UserId != null && context.Socks4.UserId.Length > 0;
        }

        private static bool IsProxyAuthorizationCredentialsCorrect(SessionContext context)
        {
            return context.ProxyAuthenticator.Authenticate(context, context.Socks4?.UserId, null);
        }
    }
}
