using Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using System.Net;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpBypassHandler : IHandler
    {
        private static readonly HttpBypassHandler Self = new();

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

            context.RemoteStream = context.RemoteClient.GetStream();

            if (context.Http?.HTTPVerb != "CONNECT")
            {
                var firstLine = $"{context.Http?.HTTPVerb} {context.Http?.GetHTTPTargetPath()} {context.Http?.HTTPProtocol}";
                var headerBytes = context.Http?.GetBytes(false, null, firstLine);
                if (headerBytes != null && headerBytes.Length > 0)
                {
                    await SendHttpHeaderBytes(context, headerBytes);
                }
            }

            return HandleStep.Tunnel;
        }
        
        public static HttpBypassHandler Instance()
        {
            return Self;
        }

        private static async Task SendConnectionEstablised(SessionContext context)
        {
            if (context.ClientStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }

        private static async Task SendHttpHeaderBytes(SessionContext context, byte[] headerBytes)
        {
            if (context.RemoteStream == null) return;
            await context.RemoteStream.WriteAsync(headerBytes, context.Token);
            context.SentCounter?.OnBytesSent(context, headerBytes.Length, headerBytes, 0);
        }
    }
}
