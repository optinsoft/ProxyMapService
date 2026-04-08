using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Proto
{
    public class HttpProto
    {
        public static async Task HttpReplyConnectionEstablished(Stream? incomingStream, CancellationToken token)
        {
            if (incomingStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
            await incomingStream.WriteAsync(bytes, token);
        }

        public static async Task HttpReplyConnectionEstablished(SessionContext context)
        {
            await HttpReplyConnectionEstablished(context.IncomingStream, context.Token);
        }

        public static async Task HttpReplyError(Stream? incomingStream, string httpStatusLine, List<string>? customHeaders, string? errorMessage, CancellationToken token)
        {
            if (incomingStream == null) return;
            List<string> headers = [
                httpStatusLine
            ];
            if (customHeaders != null)
            {
                headers.AddRange(customHeaders);
            }
            headers.Add("Connection: close");
            byte[] contentBytes = string.IsNullOrEmpty(errorMessage) ? [] : Encoding.UTF8.GetBytes(errorMessage);
            if (contentBytes.Length > 0)
            {
                headers.Add("Content-Type: text/html; charset=UTF-8");
            }
            headers.Add($"Content-Length: {contentBytes.Length}");
            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);
            byte[] bytes = contentBytes.Length > 0 ? new byte[headerBytes.Length + contentBytes.Length] : headerBytes;
            if (contentBytes.Length > 0)
            {
                Buffer.BlockCopy(headerBytes, 0, bytes, 0, headerBytes.Length);
                Buffer.BlockCopy(contentBytes, 0, bytes, headerBytes.Length, contentBytes.Length);
            }
            await incomingStream.WriteAsync(bytes, token);
        }

        public static async Task HttpReplyError(Stream? incomingStream, string httpStatusLine, List<string>? customHeaders, CancellationToken token)
        {
            await HttpReplyError(incomingStream, httpStatusLine, customHeaders, null, token);
        }

        public static async Task HttpReplyError(Stream? incomingStream, string httpStatusLine, string? errorMessage, CancellationToken token)
        {
            await HttpReplyError(incomingStream, httpStatusLine, null, errorMessage, token);
        }

        public static async Task HttpReplyError(Stream? incomingStream, string httpStatusLine, CancellationToken token)
        {
            await HttpReplyError(incomingStream, httpStatusLine, null, null, token);
        }

        public static async Task HttpReplyError(SessionContext context, string httpStatusLine, string? errorMessage = null)
        {
            await HttpReplyError(context.IncomingStream, httpStatusLine, null, errorMessage, context.Token);
        }

        public static async Task HttpReplyBadGateway(Stream? incomingStream, string? errorMessage, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 502 Bad Gateway", null, errorMessage, token);
        }

        public static async Task HttpReplyBadGateway(SessionContext context, string? errorMessage = null)
        {
            await HttpReplyBadGateway(context.IncomingStream, errorMessage, context.Token);
        }

        public static async Task HttpReplyGatewayTimeout(Stream? incomingStream, string? errorMessage, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 504 Gateway Timeout", null, errorMessage, token);
        }

        public static async Task HttpReplyGatewayTimeout(SessionContext context, string? errorMessage)
        {
            await HttpReplyGatewayTimeout(context.IncomingStream, errorMessage, context.Token);
        }

        public static async Task HttpReplyProxyAuthenticationRequired(Stream? incomingStream, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 407 Proxy Authentication Required", ["Proxy-Authenticate: Basic realm=\"Pass Through Proxy\""], token);
        }

        public static async Task HttpReplyProxyAuthenticationRequired(SessionContext context)
        {
            await HttpReplyProxyAuthenticationRequired(context.IncomingStream, context.Token);
        }

        public static async Task HttpReplyProxyUnauthorized(Stream? incomingStream, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 401 Unauthorized", ["Proxy-Authenticate: Basic realm=\"Pass Through Proxy\""], token);
        }

        public static async Task HttpReplyProxyUnauthorized(SessionContext context)
        {
            await HttpReplyProxyUnauthorized(context.IncomingStream, context.Token);
        }

        public static async Task HttpReplyBadRequest(Stream? incomingStream, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 400 Bad Request", token);
        }

        public static async Task HttpReplyBadRequest(SessionContext context)
        {
            await HttpReplyBadRequest(context.IncomingStream, context.Token);
        }

        public static async Task HttpReplyForbidden(Stream? incomingStream, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 403 Forbidden", token);
        }

        public static async Task HttpReplyForbidden(SessionContext context)
        {
            await HttpReplyForbidden(context.IncomingStream, context.Token);
        }

        public static async Task HttpReplyNotFound(Stream? incomingStream, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 404 Not Found", token);
        }

        public static async Task HttpReplyNotFound(SessionContext context)
        {
            await HttpReplyNotFound(context.IncomingStream, context.Token);
        }

        public static async Task HttpReplyMethodNotAllowed(Stream? incomingStream, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 405 Method Not Allowed", token);
        }
        
        public static async Task HttpReplyMethodNotAllowed(SessionContext context)
        {
            await HttpReplyMethodNotAllowed(context.IncomingStream, context.Token);
        }

        public static async Task HttpReplyFileStream(Stream? incomingStream, FileStream fileStream, CancellationToken token)
        {
            if (incomingStream == null) return;

            var fileInfo = new FileInfo(fileStream.Name);
            string contentType = GetContentType(fileInfo.Extension);

            List<string> headers = [
                "HTTP/1.1 200 OK",
                "Connection: close",
                $"Content-Length: {fileInfo.Length}",
                $"Content-Type: {contentType}"
            ];
            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);

            await incomingStream.WriteAsync(headerBytes, token);
            await fileStream.CopyToAsync(incomingStream, token);
        }

        public static async Task HttpReplyFileStream(SessionContext context, FileStream fileStream)
        {
            await HttpReplyFileStream(context.IncomingStream, fileStream, context.Token);
        }

        public static async Task HttpReplyCacheFileStream(SessionContext context, CountingStream? incomingStream,
            FileStream fileStream)
        {
            if (incomingStream == null) return;
            var oldCached = context.CachedReply;
            context.CachedReply = true;
            try
            {
                await fileStream.CopyToAsync(incomingStream, context.Token);
            }
            finally
            {
                context.CachedReply = oldCached;
            }
        }

        public static async Task HttpReplyCacheFileStream(SessionContext context, FileStream fileStream)
        {
            await HttpReplyCacheFileStream(context, context.IncomingStream, fileStream);
        }

        public static async Task SendHttpRequest(Stream? outgoingStream, byte[] requestBytes, CancellationToken token)
        {
            if (outgoingStream == null) return;
            await outgoingStream.WriteAsync(requestBytes, token);
        }

        public static async Task SendHttpRequest(SessionContext context, byte[] requestBytes)
        {
            await SendHttpRequest(context.OutgoingStream, requestBytes, context.Token);
        }

        private static string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".html" => "text/html; charset=utf-8",
                ".htm" => "text/html; charset=utf-8",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".txt" => "text/plain; charset=utf-8",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            };
        }
    }
}
