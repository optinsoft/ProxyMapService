using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class ProxyHandler : BaseProxyHandler, IHandler
    {
        private static readonly ProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Proxified = true;

            context.SessionsCounter?.OnHostProxified(context);

            context.ProxyServer ??= context.ProxyProvider.GetProxyServer(context);

            try
            {
                IPEndPoint outgoingEndPoint = Address.GetIPEndPoint(context.ProxyServer.Host, context.ProxyServer.Port);
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnProxyFailed(context);
                if (context.Http != null)
                {
                    await HttpReply(context, Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n"));
                }
                if (context.Socks4 != null)
                {
                    await Socks4Reply(context, Socks4Command.RequestRejectedOrFailed);
                }
                if (context.Socks5 != null)
                {
                    await Socks5Reply(context, Socks5Status.GeneralFailure);
                }
                throw;
            }

            context.SessionsCounter?.OnProxyConnected(context);

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
