using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using static System.Net.WebRequestMethods;
using System;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5HostActionHandler : BaseHostActionHandler, IHandler
    {
        private static readonly Socks5HostActionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Socks5?.Host == null || context.Socks5.Host.Hostname.Length == 0)
            {
                context.SessionsCounter?.OnNoHost(context);
                await SendSocks5Reply(context, Socks5Status.HostUnreachable);
                return HandleStep.Terminate;
            }

            context.HostName = context.Socks5.Host.Hostname;
            context.HostPort = context.Socks5.Host.Port;

            HostRule? rule;
            context.HostAction = GetHostAction(context.HostName, context.HostRules, out rule);
            if (context.HostAction == ActionEnum.Deny)
            {
                context.SessionsCounter?.OnHostRejected(context);
                await SendSocks5Reply(context, Socks5Status.ConnectionNotAllowed);
                return HandleStep.Terminate;
            }
            if (rule?.HostName != null)
            {
                context.HostName = rule.HostName;
            }
            if (rule?.HostPort != null)
            {
                context.HostPort = rule.HostPort.Value;
            }

            return context.HostAction == ActionEnum.Bypass ? HandleStep.Socks5Bypass : HandleStep.Socks5Proxy;
        }

        public static Socks5HostActionHandler Instance()
        {
            return Self;
        }

        private static async Task SendSocks5Reply(SessionContext context, Socks5Status status)
        {
            if (context.ClientStream == null) return;
            byte[] bytes = [0x05, (byte)status, 0x0, 0x01, 0x0, 0x0, 0x0, 0x0, 0x10, 0x10];
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }
    }
}
