using Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
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

            IPEndPoint remoteEndPoint = Address.GetIPEndPoint(context.HostName, context.HostPort);

            try
            {
                await context.RemoteClient.ConnectAsync(remoteEndPoint, context.Token);
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                await SendSocks5Reply(context, Socks5Status.NetworkUnreachable);
                throw;
            }

            await SendSocks5Reply(context, Socks5Status.Succeeded);

            context.SessionsCounter?.OnBypassConnected(context);

            context.RemoteStream = context.RemoteClient.GetStream();

            return HandleStep.Tunnel;
        }

        public static Socks5BypassHandler Instance()
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
