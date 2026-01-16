using ProxyMapService.Proxy.Configurations;
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
                await SendSocks4Reply(context, Socks4Command.RequestRejectedOrFailed);
                return HandleStep.Terminate;
            }

            context.HostName = context.Socks4.Host.Hostname;
            context.HostPort = context.Socks4.Host.Port;

            GetContextHostAction(context);

            if (context.HostAction == ActionEnum.Deny)
            {
                context.SessionsCounter?.OnHostRejected(context);
                await SendSocks4Reply(context, Socks4Command.RequestRejectedOrFailed);
                return HandleStep.Terminate;
            }

            return context.HostAction == ActionEnum.Bypass ? HandleStep.Socks4Bypass : HandleStep.Proxy;
        }

        public static Socks4HostActionHandler Instance()
        {
            return Self;
        }

        private static async Task SendSocks4Reply(SessionContext context, Socks4Command command)
        {
            if (context.IncomingStream == null) return;
            byte[] bytes = [0x0, (byte)command, 0, 0, 0, 0, 0, 0];
            if (context.Socks4 != null)
            {
                Array.Copy(context.Socks4.Bytes, 2, bytes, 2, 6);
            }
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }
    }
}
