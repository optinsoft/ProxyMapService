using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using ProxyMapService.Proxy.Counters;
using System.Net;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5BypassHandler : IHandler
    {
        private static readonly Socks5BypassHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Bypassed = true;

            context.SessionsCounter?.OnHostBypassed(context);

            IPEndPoint outgoingEndPoint = Address.GetIPEndPoint(context.HostName, context.HostPort);

            try
            {
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                await SendSocks5Reply(context, Socks5Status.NetworkUnreachable);
                throw;
            }

            context.SessionsCounter?.OnBypassConnected(context);

            context.CreateOutgoingClientStream();

            await SendSocks5Reply(context, Socks5Status.Succeeded);

            return HandleStep.Tunnel;
        }

        public static Socks5BypassHandler Instance()
        {
            return Self;
        }

        private static async Task SendSocks5Reply(SessionContext context, Socks5Status status)
        {
            if (context.IncomingStream == null) return;
            byte[] bytes = [0x05, (byte)status, 0x0, 0x01, 0x0, 0x0, 0x0, 0x0, 0x10, 0x10];
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }
    }
}
