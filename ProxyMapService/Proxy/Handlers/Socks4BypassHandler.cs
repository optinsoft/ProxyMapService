using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using ProxyMapService.Proxy.Counters;
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

            try
            {
                IPEndPoint outgoingEndPoint = context.Host.GetIPEndPoint();
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                await Socks4Reply(context, Socks4Command.RequestRejectedOrFailed);
                throw;
            }

            context.SessionsCounter?.OnBypassConnected(context);

            context.CreateOutgoingClientStream();

            await Socks4Reply(context, Socks4Command.RequestGranted);

            return HandleStep.Tunnel;
        }

        public static Socks4BypassHandler Instance()
        {
            return Self;
        }

        private static async Task Socks4Reply(SessionContext context, Socks4Command command)
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
