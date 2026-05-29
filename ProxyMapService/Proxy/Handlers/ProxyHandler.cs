using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;

namespace ProxyMapService.Proxy.Handlers
{
    public class ProxyHandler : BaseProxyHandler, IHandler
    {
        private static readonly ProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Proxified = true;

            context.ProxyCounters.SessionsCounter?.OnHostProxified(context);

            context.ProxyServer ??= context.ProxyProvider.GetProxyServer(context);

            System.Net.IPEndPoint? outgoingEndPoint = null;

            try
            {
                outgoingEndPoint = await HostAddress.GetIPEndPoint(context.ProxyServer.Host, context.ProxyServer.Port);
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (Exception ex)
            {
                if (outgoingEndPoint == null)
                {
                    context.Logger.LogHostError(ex.Message, context.Host.Hostname);
                }
                else
                {
                    context.Logger.LogProxyServerConnectionFailed(ex.Message, outgoingEndPoint, context.ProxyServer);
                }
                context.ProxyCounters.SessionsCounter?.OnProxyFailed(context);
                await (context switch
                {
                    { Http: not null } => HttpProto.HttpReplyBadGateway(context),
                    { Socks4: not null } => Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed),
                    { Socks5: not null } => Socks5Proto.Socks5ReplyStatus(context, Socks5Status.GeneralFailure),
                    _ => Task.CompletedTask
                });
                return HandleStep.Terminate;
            }

            switch (context.ProxyServer.ProxyType)
            {
                case ProxyType.Http:
                    context.Logger.LogHttpProxyServerConnected(context.OutgoingClient, context.ProxyServer);
                    break;
                case ProxyType.Socks4:
                    context.Logger.LogSocks4ProxyServerConnected(context.OutgoingClient, context.ProxyServer);
                    break;
                case ProxyType.Socks5:
                    context.Logger.LogSocks5ProxyServerConnected(context.OutgoingClient, context.ProxyServer);
                    break;
            }

            context.ProxyCounters.SessionsCounter?.OnProxyConnected(context);

            context.CreateOutgoingClientStream();
            if (context.OutgoingStream != null)
            {
                context.OutgoingStream.DisconnectHandler += HandlerLogger.OnProxyServerDisconnected;
            }

            switch (context.ProxyServer.ProxyType)
            {
                case ProxyType.Http:
                    return HandleStep.HttpProxy;
                case ProxyType.Socks4:
                    return HandleStep.Socks4Proxy;
                case ProxyType.Socks5:
                    return HandleStep.Socks5Proxy;
                default:
                    break;
            }

            return HandleStep.Terminate;
        }

        public static ProxyHandler Instance()
        {
            return Self;
        }
    }
}
