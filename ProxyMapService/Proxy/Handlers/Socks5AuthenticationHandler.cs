using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5AuthenticationHandler : IHandler
    {
        private static readonly Socks5AuthenticationHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (!IsAuthenticationRequired(context))
            {
                if (IsMethodPresent(context, 0x02))
                {
                    context.SessionsCounter?.OnAuthenticationRequired(context);
                    await SendSelectMethod(context, 0x02);
                    return HandleStep.Socks5UsernamePasswordAuthentication;
                }
                context.SessionsCounter?.OnAuthenticationNotRequired(context);
                if (!IsMethodPresent(context, 0x0))
                {
                    await SendNoMethod(context);
                    return HandleStep.Terminate;
                }
                await SendSelectMethod(context, 0x0);
                return HandleStep.Socks5AuthenticationNotRequired;
            }
            context.SessionsCounter?.OnAuthenticationRequired(context);
            if (!IsMethodPresent(context, 0x02))
            {
                await SendNoMethod(context);
                return HandleStep.Terminate;
            }
            await SendSelectMethod(context, 0x02);
            return HandleStep.Socks5UsernamePasswordAuthentication;
        }

        public static Socks5AuthenticationHandler Instance()
        {
            return Self;
        }

        private static bool IsAuthenticationRequired(SessionContext context)
        {
            return context.Mapping.Authentication.Required;
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

        private static async Task SendNoMethod(SessionContext context)
        {
            await SendSelectMethod(context, 0xff);
        }

        private static async Task SendSelectMethod(SessionContext context, byte method)
        {
            if (context.ClientStream == null) return;
            byte[] bytes = [0x05, method];
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }
    }
}
