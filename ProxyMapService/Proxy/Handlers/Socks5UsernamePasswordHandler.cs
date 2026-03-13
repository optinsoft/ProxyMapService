using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Proto;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5UsernamePasswordHandler : BaseAuthenticationHandler, IHandler
    {
        private static readonly Socks5UsernamePasswordHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream == null)
            {
                throw new NullClientStreamException();
            }
            byte[]? bytesArray = await Socks5Proto.ReadUsernamePassword(context);
            if (!(context.Socks5?.ParseUsernamePassword(bytesArray) ?? false))
            {
                context.ProxyCounters.SessionsCounter?.OnSocks5Failure(context);
                await Socks5Proto.Socks5ReplyNotAuthenticated(context);
                return HandleStep.Terminate;
            }

            if (IsProxyAuthorizationCredentialsCorrect(context))
            {
                OnAuthenticated(context);
                await Socks5Proto.Socks5ReplyAuthenticated(context);
                return HandleStep.Socks5Authenticated;
            }

            OnAuthenticationInvalid(context);
            await Socks5Proto.Socks5ReplyNotAuthenticated(context);
            return HandleStep.Terminate;
        }

        public static Socks5UsernamePasswordHandler Instance()
        {
            return Self;
        }
        
        private static bool IsProxyAuthorizationCredentialsCorrect(SessionContext context)
        {
            return context.ProxyAuthenticator.Authenticate(context, context.Socks5?.Username, context.Socks5?.Password);
        }        
    }
}
