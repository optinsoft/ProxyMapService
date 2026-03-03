using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Proto
{
    public class HttpProto
    {
        public static async Task HttpReplyConnectionEstablished(SessionContext context)
        {
            if (context.IncomingStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        public static async Task HttpReplyError(SessionContext context, string httpStatusLine, string? errorMessage = null)
        {
            if (context.IncomingStream == null) return;
            List<string> headers = [
                httpStatusLine,
                "Connection: close"
            ];
            byte[] contentBytes = string.IsNullOrEmpty(errorMessage) ? [] : Encoding.UTF8.GetBytes(errorMessage);
            if (contentBytes.Length > 0)
            {
                headers.Add("Content-Type: text/html; charset=UTF-8");
                headers.Add($"Content-Length: {contentBytes.Length}");
            }
            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);
            byte[] bytes = contentBytes.Length > 0 ? new byte[headerBytes.Length + contentBytes.Length] : headerBytes;
            if (contentBytes.Length > 0)
            {
                Buffer.BlockCopy(headerBytes, 0, bytes, 0, headerBytes.Length);
                Buffer.BlockCopy(contentBytes, 0, bytes, headerBytes.Length, contentBytes.Length);
            }
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        public static async Task HttpReplyBadGateway(SessionContext context, string? errorMessage = null)
        {
            await HttpReplyError(context, "HTTP/1.1 502 Bad Gateway", errorMessage);
        }

        public static async Task HttpReplyGatewayTimeout(SessionContext context, string? errorMessage = null)
        {
            await HttpReplyError(context, "HTTP/1.1 504 Gateway Timeout", errorMessage);
        }

        public static async Task HttpReplyProxyAuthenticationRequired(SessionContext context)
        {
            if (context.IncomingStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"Pass Through Proxy\"\r\nConnection: close\r\n\r\n");
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        public static async Task HttpReplyProxyUnauthorized(SessionContext context)
        {
            if (context.IncomingStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 401 Unauthorized\r\nProxy-Authenticate: Basic realm=\"Pass Through Proxy\"\r\nConnection: close\r\n\r\n");
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        public static async Task HttpReplyBadRequest(SessionContext context)
        {
            await HttpReplyError(context, "HTTP/1.1 400 Bad Request");
        }

        public static async Task HttpReplyForbidden(SessionContext context)
        {
            await HttpReplyError(context, "HTTP/1.1 403 Forbidden");
        }

        public static async Task HttpReplyMethodNotAllowed(SessionContext context)
        {
            await HttpReplyError(context, "HTTP/1.1 405 Method Not Allowed");
        }

        public static async Task SendHttpRequest(SessionContext context, byte[] requestBytes)
        {
            if (context.OutgoingStream == null) return;
            await context.OutgoingStream.WriteAsync(requestBytes, context.Token);
        }
    }
}
