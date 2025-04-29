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
    public class HttpProxyHandler : IHandler
    {
        private static readonly HttpProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Proxified = true;

            context.SessionsCounter?.OnHostProxified(context);

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

            context.RemoteStream = context.RemoteClient.GetStream();

            string? proxyAuthorization = !context.Mapping.Authentication.SetHeader
                ? null
                : Convert.ToBase64String(Encoding.ASCII.GetBytes($"{context.Mapping.Authentication.Username}:{context.Mapping.Authentication.Password}"));

            var headerBytes = context.Http?.GetBytes(true, proxyAuthorization, null);
            if (headerBytes != null && headerBytes.Length > 0)
            {
                await SendHttpHeaderBytes(context, headerBytes);
            }

            return HandleStep.Tunnel;
        }

        public static HttpProxyHandler Instance()
        {
            return Self;
        }

        private static async Task SendHttpHeaderBytes(SessionContext context, byte[] headerBytes)
        {
            if (context.RemoteStream == null) return;
            await context.RemoteStream.WriteAsync(headerBytes, context.Token);
            context.SentCounter?.OnBytesSent(context, headerBytes.Length, headerBytes, 0);
        }
    }
}
