using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4AuthenticationHandler : IHandler
    {
        private static readonly Socks4AuthenticationHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (!IsUserIdPresent(context))
            {
                if (!IsAuthenticationRequired(context))
                {
                    context.SessionsCounter?.OnAuthenticationNotRequired(context);
                    return HandleStep.Socks4AuthenticationNotRequired;
                }
                context.SessionsCounter?.OnAuthenticationRequired(context);
                await SendSocks4Reply(context, Socks4Command.RequestRejectedOrFailed);
                return HandleStep.Terminate;
            }

            if (IsProxyAuthorizationCredentialsCorrect(context))
            {
                context.SessionsCounter?.OnAuthenticated(context);
                return HandleStep.Socks4Authenticated;
            }

            context.SessionsCounter?.OnAuthenticationInvalid(context);
            await SendSocks4Reply(context, Socks4Command.RequestRejectedOrFailed);
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
            return context.ProxyAuthenticator.Authenticate(context.Socks4?.UserId, null);
        }

        private static async Task SendSocks4Reply(SessionContext context, Socks4Command command)
        {
            if (context.ClientStream == null) return;
            byte[] bytes = [0x0, (byte)command, 0, 0, 0, 0, 0, 0];
            if (context.Socks4 != null)
            {
                Array.Copy(context.Socks4.Bytes, 2, bytes, 2, 6);
            }
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }
    }
}
