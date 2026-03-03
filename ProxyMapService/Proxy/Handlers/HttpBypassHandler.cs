using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using System.Net;
using System.Net.Sockets;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpBypassHandler : IHandler
    {
        private static readonly HttpBypassHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Bypassed = true;

            context.SessionsCounter?.OnHostBypassed(context);

            try
            {
                IPEndPoint outgoingEndPoint = context.Host.GetIPEndPoint();
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (SocketException ex)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                switch (ex.SocketErrorCode)
                {
                    case SocketError.TimedOut:
                    case SocketError.TryAgain:
                        await HttpProto.HttpReplyGatewayTimeout(context, $"SocketError {ex.SocketErrorCode}");
                        throw;
                    default:
                        await HttpProto.HttpReplyBadGateway(context, $"SocketError {ex.SocketErrorCode}");
                        throw;
                }
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                await HttpProto.HttpReplyBadGateway(context);
                throw;
            }

            context.SessionsCounter?.OnBypassConnected(context);

            context.CreateOutgoingClientStream();

            if (context.Http?.HTTPVerb == "CONNECT")
            {
                await HttpProto.HttpReplyConnectionEstablished(context);
            }
            else
            {
                var requestFirstLine = $"{context.Http?.HTTPVerb} {context.Http?.HTTPTargetPath} {context.Http?.HTTPProtocol}";
                var httpRequestBytes = context.Http?.GetBytes(false, null, requestFirstLine);
                if (httpRequestBytes != null && httpRequestBytes.Length > 0)
                {
                    await HttpProto.SendHttpRequest(context, httpRequestBytes);
                }
            }

            return HandleStep.Tunnel;
        }
        
        public static HttpBypassHandler Instance()
        {
            return Self;
        }
    }
}
