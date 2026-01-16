using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Counters;
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

            IPEndPoint outgoingEndPoint = Address.GetIPEndPoint(context.HostName, context.HostPort);

            try
            {
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                throw;
            }

            context.SessionsCounter?.OnBypassConnected(context);

            context.CreateOutgoingClientStream();

            if (context.Http?.HTTPVerb == "CONNECT")
            {
                await SendConnectionEstablised(context);
            }
            else
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
            if (context.IncomingStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        private static async Task SendHttpHeaderBytes(SessionContext context, byte[] headerBytes)
        {
            if (context.OutgoingStream == null) return;
            await context.OutgoingStream.WriteAsync(headerBytes, context.Token);
        }
    }
}
