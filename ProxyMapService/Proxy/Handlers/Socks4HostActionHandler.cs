using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4HostActionHandler : BaseHostActionHandler, IHandler
    {
        private static readonly Socks4HostActionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Socks4?.Host == null || context.Socks4.Host.Hostname.Length == 0)
            {
                context.SessionsCounter?.OnNoHost(context);
                await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed);
                return HandleStep.Terminate;
            }

            context.Host = context.Socks4.Host;

            GetContextHostAction(context);

            switch (context.HostAction)
            {
                case ActionEnum.Allow:
                    return HandleStep.Proxy;
                case ActionEnum.Bypass:
                    return HandleStep.Socks4Bypass;
                case ActionEnum.File:
                    return HandleStep.Socks4File;
                default:
                    //ActionEnum.Deny
                    context.SessionsCounter?.OnHostRejected(context);
                    await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed);
                    return HandleStep.Terminate;
            }
        }

        public static Socks4HostActionHandler Instance()
        {
            return Self;
        }
    }
}
