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

            try
            {
                System.Net.IPEndPoint outgoingEndPoint = HostAddress.GetIPEndPoint(context.ProxyServer.Host, context.ProxyServer.Port);
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (Exception)
            {
                context.ProxyCounters.SessionsCounter?.OnProxyFailed(context);
                if (context.Http != null)
                {
                    await HttpProto.HttpReplyBadGateway(context);
                }
                if (context.Socks4 != null)
                {
                    await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed);
                }
                if (context.Socks5 != null)
                {
                    await Socks5Proto.Socks5ReplyStatus(context, Socks5Status.GeneralFailure);
                }
                throw;
            }

            context.ProxyCounters.SessionsCounter?.OnProxyConnected(context);

            context.CreateOutgoingClientStream();

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
