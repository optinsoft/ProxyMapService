using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using ProxyMapService.Proxy.Proto;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5HostActionHandler : BaseHostActionHandler, IHandler
    {
        private static readonly Socks5HostActionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Socks5?.Host == null || context.Socks5.Host.Hostname.Length == 0)
            {
                context.ProxyCounters.SessionsCounter?.OnNoHost(context);
                await Socks5Proto.Socks5ReplyStatus(context, Socks5Status.HostUnreachable);
                return HandleStep.Terminate;
            }

            context.Host = context.Socks5.Host;

            GetContextHostAction(context);

            switch (context.HostAction)
            {
                case ActionEnum.Allow:
                    return HandleStep.Proxy;
                case ActionEnum.Bypass:
                    return HandleStep.Socks5Bypass;
                case ActionEnum.File:
                    return HandleStep.Socks5File;
                default:
                    //ActionEnum.Deny
                    context.ProxyCounters.SessionsCounter?.OnHostRejected(context);
                    await Socks5Proto.Socks5ReplyStatus(context, Socks5Status.ConnectionNotAllowed);
                    return HandleStep.Terminate;
            }
        }

        public static Socks5HostActionHandler Instance()
        {
            return Self;
        }
    }
}
