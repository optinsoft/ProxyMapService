using Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using System.Net.Sockets;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Handlers
{
    public class ProxyHandler : IHandler
    {
        private static readonly ProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Proxified = true;

            context.SessionsCounter?.OnHostProxified(context);

            string? proxyAuthorization = !context.Mapping.Authentication.SetHeader 
                ? null 
                : Convert.ToBase64String(Encoding.ASCII.GetBytes($"{context.Mapping.Authentication.Username}:{context.Mapping.Authentication.Password}"));

            context.TunnelHeaderBytes = context.Header?.GetBytes(true, proxyAuthorization);

            IPEndPoint remoteEndPoint = Address.GetIPEndPoint(context.Mapping.ProxyServer.Host, context.Mapping.ProxyServer.Port);

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

            return HandleStep.Tunnel;
        }

        public static ProxyHandler Instance()
        {
            return Self;
        }
    }
}
