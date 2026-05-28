using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using ProxyMapService.Proxy.Proto;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4BypassHandler : IHandler
    {
        private static readonly Socks4BypassHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Bypassed = true;

            context.ProxyCounters.SessionsCounter?.OnHostBypassed(context);

            System.Net.IPEndPoint outgoingEndPoint;

            try
            {
                outgoingEndPoint = await context.Host.GetIPEndPoint();
            }
            catch (Exception ex)
            {
                context.Logger.LogHostError(ex.Message, context.Host.Hostname);
                context.ProxyCounters.SessionsCounter?.OnBypassFailed(context);
                await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed);
                return HandleStep.Terminate;
            }

            try
            {
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (Exception)
            {
                context.ProxyCounters.SessionsCounter?.OnBypassFailed(context);
                await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed);
                throw;
            }

            context.Logger.LogBypassServerConnected(context.OutgoingClient, context.Host);

            context.ProxyCounters.SessionsCounter?.OnBypassConnected(context);

            context.CreateOutgoingClientStream();
            if (context.OutgoingStream != null)
            {
                context.OutgoingStream.DisconnectHandler += HandlerLogger.OnBypassServerDisconnected;
            }

            await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestGranted);

            return HandleStep.Tunnel;
        }

        public static Socks4BypassHandler Instance()
        {
            return Self;
        }
    }
}
