using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4BypassHandler : IHandler
    {
        private static readonly Socks4BypassHandler Self = new();

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
                await SendSocks4Reply(context, Socks4Command.RequestRejectedOrFailed);
                throw;
            }

            context.SessionsCounter?.OnBypassConnected(context);

            context.RemoteStream = context.RemoteClient.GetStream();

            await SendSocks4Reply(context, Socks4Command.RequestGranted);

            return HandleStep.Tunnel;
        }

        public static Socks4BypassHandler Instance()
        {
            return Self;
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
