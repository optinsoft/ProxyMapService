using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Counters;
using System.Net;

namespace ProxyMapService.Proxy.Handlers
{
    public class ProxyHandler : BaseProxyHandler, IHandler
    {
        private static readonly ProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Proxified = true;

            context.SessionsCounter?.OnHostProxified(context);

            if (context.ProxyServer == null)
            {
                context.ProxyServer = context.ProxyProvider.GetProxyServer();
            }

            IPEndPoint remoteEndPoint = Address.GetIPEndPoint(context.ProxyServer.Host, context.ProxyServer.Port);

            try
            {
                await context.RemoteClient.ConnectAsync(remoteEndPoint, context.Token);
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnProxyFailed(context);
                throw;
            }

            context.SessionsCounter?.OnProxyConnected(context);

            context.CreateRemoteClientStream();

            switch (context.ProxyServer.ProxyType)
            {
                case ProxyType.Http:
                    return HandleStep.HttpProxy;
                case ProxyType.Socks4:
                    return HandleStep.Socks4Proxy;
                case ProxyType.Socks5:
                    return HandleStep.Socks5Proxy;
            }

            return HandleStep.Terminate;
        }

        public static ProxyHandler Instance()
        {
            return Self;
        }
    }
}
