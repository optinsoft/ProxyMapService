using Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using System.Net;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class BypassHandler : IHandler
    {
        private static readonly BypassHandler Self = new();

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
                throw;
            }

            await SendConnectionEstablised(context);

            context.SessionsCounter?.OnBypassConnected(context);

            return HandleStep.Tunnel;
        }

        private static async Task SendConnectionEstablised(SessionContext context)
        {
            if (context.ClientStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }

        public static BypassHandler Instance()
        {
            return Self;
        }
    }
}
