using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5AuthenticationHandler : BaseAuthenticationHandler, IHandler
    {
        private static readonly Socks5AuthenticationHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (!IsAuthenticationRequired(context))
            {
                if (IsMethodPresent(context, 0x02))
                {
                    OnAuthenticationRequired(context);
                    await Socks5Proto.Socks5ReplySelectMethod(context, 0x02);
                    return HandleStep.Socks5UsernamePasswordAuthentication;
                }
                OnAuthenticationNotRequired(context);
                if (!IsMethodPresent(context, 0x0))
                {
                    await Socks5Proto.Socks5ReplyNoMethod(context);
                    return HandleStep.Terminate;
                }
                await Socks5Proto.Socks5ReplySelectMethod(context, 0x0);
                return HandleStep.Socks5AuthenticationNotRequired;
            }
            OnAuthenticationRequired(context);
            if (!IsMethodPresent(context, 0x02))
            {
                await Socks5Proto.Socks5ReplyNoMethod(context);
                return HandleStep.Terminate;
            }
            await Socks5Proto.Socks5ReplySelectMethod(context, 0x02);
            return HandleStep.Socks5UsernamePasswordAuthentication;
        }

        public static Socks5AuthenticationHandler Instance()
        {
            return Self;
        }

        private static bool IsAuthenticationRequired(SessionContext context)
        {
            return context.ProxyAuthenticator.Required;
        }

        private static bool IsMethodPresent(SessionContext context, byte method)
        {
            if (context.Socks5?.Methods == null) return false;
            for (int i = 0; i < (int)context.Socks5.NMethods; ++i)
            {
                if (context.Socks5.Methods[i] == method)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
