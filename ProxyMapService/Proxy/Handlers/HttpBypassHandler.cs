using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

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
                IPEndPoint outgoingEndPoint = Address.GetIPEndPoint(context.HostName, context.HostPort);
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (SocketException ex)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                switch (ex.SocketErrorCode)
                {
                    case SocketError.TimedOut:
                    case SocketError.TryAgain:
                        await HttpReplyGatewayTimeout(context, $"SocketError {ex.SocketErrorCode}");
                        throw;
                    default:
                        await HttpReplyBadGateway(context, $"SocketError {ex.SocketErrorCode}");
                        throw;
                }
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                await HttpReplyBadGateway(context);
                throw;
            }

            context.SessionsCounter?.OnBypassConnected(context);

            context.CreateOutgoingClientStream();

            if (context.Http?.HTTPVerb == "CONNECT")
            {
                await HttpReplyConnectionEstablised(context);
            }
            else
            {
                var firstLine = $"{context.Http?.HTTPVerb} {context.Http?.HTTPTargetPath} {context.Http?.HTTPProtocol}";
                var headerBytes = context.Http?.GetBytes(false, null, firstLine);
                if (headerBytes != null && headerBytes.Length > 0)
                {
                    await SendHttpRequest(context, headerBytes);
                }
            }

            return HandleStep.Tunnel;
        }
        
        public static HttpBypassHandler Instance()
        {
            return Self;
        }

        private static async Task HttpReplyConnectionEstablised(SessionContext context)
        {
            if (context.IncomingStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        private static async Task HttpReplyError(SessionContext context, string httpStatusLine, string? errorMessage = null)
        {
            if (context.IncomingStream == null) return;
            List<string> headers = [
                httpStatusLine,
                "Connection: close"
            ];
            byte[] contentBytes = String.IsNullOrEmpty(errorMessage) ? [] : Encoding.UTF8.GetBytes(errorMessage);
            if (contentBytes.Length > 0)
            {
                headers.Add("Content-Type: text/html; charset=UTF-8");
                headers.Add($"Content-Length: {contentBytes.Length}");
            }
            var headerText = String.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);
            byte[] bytes = contentBytes.Length > 0 ? new byte[headerBytes.Length + contentBytes.Length] : headerBytes;
            if (contentBytes.Length > 0)
            {
                Buffer.BlockCopy(headerBytes, 0, bytes, 0, headerBytes.Length);
                Buffer.BlockCopy(contentBytes, 0, bytes, headerBytes.Length, contentBytes.Length);
            }
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        private static async Task HttpReplyBadGateway(SessionContext context, string? errorMessage = null)
        {
            await HttpReplyError(context, "HTTP/1.1 502 Bad Gateway", errorMessage);
        }

        private static async Task HttpReplyGatewayTimeout(SessionContext context, string? errorMessage = null)
        {
            await HttpReplyError(context, "HTTP/1.1 504 Gateway Timeout", errorMessage);
        }

        private static async Task SendHttpRequest(SessionContext context, byte[] requestBytes)
        {
            if (context.OutgoingStream == null) return;
            await context.OutgoingStream.WriteAsync(requestBytes, context.Token);
        }
    }
}
