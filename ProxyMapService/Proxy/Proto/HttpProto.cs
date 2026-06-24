using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using System.Text;
using System.Text.Json;

namespace ProxyMapService.Proxy.Proto
{
    public class HttpProto
    {
        public static async Task HttpReplyConnectionEstablished(Stream? incomingStream, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            if (incomingStream == null) return;
            string[] headers = [
                "HTTP/1.1 200 Connection established"
            ];
            httpLoggersProvider?.ResponseHeadersLogger?.OnHttpHeader(httpLoggersProvider, headers);
            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var bytes = Encoding.ASCII.GetBytes(headerText);
            await incomingStream.WriteAsync(bytes, token);
        }

        public static async Task HttpReplyConnectionEstablished(SessionContext context)
        {
            await HttpReplyConnectionEstablished(context.IncomingStream, context, context.Token);
        }

        public static async Task HttpReplyError(Stream? incomingStream, string httpStatusLine, List<string>? customHeaders, string? errorMessage, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
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
            httpLoggersProvider?.ResponseHeadersLogger?.OnHttpHeader(httpLoggersProvider, [.. headers]);
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

        public static async Task HttpReplyError(Stream? incomingStream, string httpStatusLine, List<string>? customHeaders,
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, httpStatusLine, customHeaders, null, httpLoggersProvider, token);
        }

        public static async Task HttpReplyError(Stream? incomingStream, string httpStatusLine, string? errorMessage,
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, httpStatusLine, null, errorMessage, httpLoggersProvider, token);
        }

        public static async Task HttpReplyError(Stream? incomingStream, string httpStatusLine,
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, httpStatusLine, null, null, httpLoggersProvider, token);
        }

        public static async Task HttpReplyError(SessionContext context, string httpStatusLine, string? errorMessage = null)
        {
            await HttpReplyError(context.IncomingStream, httpStatusLine, null, errorMessage, context, context.Token);
        }

        public static async Task HttpReplyBadGateway(Stream? incomingStream, string? errorMessage,
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 502 Bad Gateway", null, errorMessage, httpLoggersProvider, token);
        }

        public static async Task HttpReplyBadGateway(SessionContext context, string? errorMessage = null)
        {
            await HttpReplyBadGateway(context.IncomingStream, errorMessage, context, context.Token);
        }

        public static async Task HttpReplyGatewayTimeout(Stream? incomingStream, string? errorMessage,
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 504 Gateway Timeout", null, errorMessage, httpLoggersProvider, token);
        }

        public static async Task HttpReplyGatewayTimeout(SessionContext context, string? errorMessage)
        {
            await HttpReplyGatewayTimeout(context.IncomingStream, errorMessage, context, context.Token);
        }

        public static async Task HttpReplyProxyAuthenticationRequired(Stream? incomingStream, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 407 Proxy Authentication Required", 
                ["Proxy-Authenticate: Basic realm=\"Pass Through Proxy\""], httpLoggersProvider, token);
        }

        public static async Task HttpReplyProxyAuthenticationRequired(SessionContext context)
        {
            await HttpReplyProxyAuthenticationRequired(context.IncomingStream, context, context.Token);
        }

        public static async Task HttpReplyProxyUnauthorized(Stream? incomingStream, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 401 Unauthorized", 
                ["Proxy-Authenticate: Basic realm=\"Pass Through Proxy\""], httpLoggersProvider, token);
        }

        public static async Task HttpReplyProxyUnauthorized(SessionContext context)
        {
            await HttpReplyProxyUnauthorized(context.IncomingStream, context, context.Token);
        }

        public static async Task HttpReplyBadRequest(Stream? incomingStream, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 400 Bad Request", httpLoggersProvider, token);
        }

        public static async Task HttpReplyBadRequest(SessionContext context)
        {
            await HttpReplyBadRequest(context.IncomingStream, context, context.Token);
        }

        public static async Task HttpReplyForbidden(Stream? incomingStream, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 403 Forbidden", httpLoggersProvider, token);
        }

        public static async Task HttpReplyForbidden(SessionContext context)
        {
            await HttpReplyForbidden(context.IncomingStream, context, context.Token);
        }

        public static async Task HttpReplyNotFound(Stream? incomingStream, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 404 Not Found", httpLoggersProvider, token);
        }

        public static async Task HttpReplyNotFound(SessionContext context)
        {
            await HttpReplyNotFound(context.IncomingStream, context, context.Token);
        }

        public static async Task HttpReplyMethodNotAllowed(Stream? incomingStream, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyError(incomingStream, "HTTP/1.1 405 Method Not Allowed", httpLoggersProvider, token);
        }
        
        public static async Task HttpReplyMethodNotAllowed(SessionContext context)
        {
            await HttpReplyMethodNotAllowed(context.IncomingStream, context, context.Token);
        }

        public static async Task HttpReplyText(Stream? incomingStream, string text, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            if (incomingStream == null) return;

            byte[] textBytes = Encoding.UTF8.GetBytes(text);

            string[] headers = [
                "HTTP/1.1 200 OK",
                $"Date: {DateTime.UtcNow:R}",
                "Connection: close",
                $"Content-Length: {textBytes.Length}",
                "Content-Type: text/plain; charset=utf-8"
            ];

            httpLoggersProvider?.ResponseHeadersLogger?.OnHttpHeader(httpLoggersProvider,headers);

            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);

            await incomingStream.WriteAsync(headerBytes, token);
            await incomingStream.WriteAsync(textBytes, token);
        }

        public static async Task HttpReplyJson(Stream? incomingStream, object data, string[]? customHeaders, 
            JsonSerializerOptions serializerOptions, IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            if (incomingStream == null) return;

            byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(data, serializerOptions);

            string[] headers = [
                "HTTP/1.1 200 OK",
                $"Date: {DateTime.UtcNow:R}",
                "Connection: close",
                $"Content-Length: {jsonBytes.Length}",
                "Content-Type: application/json; charset=utf-8",
                .. (customHeaders ?? [])
            ];

            httpLoggersProvider?.ResponseHeadersLogger?.OnHttpHeader(httpLoggersProvider, headers);

            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);

            await incomingStream.WriteAsync(headerBytes, token);
            await incomingStream.WriteAsync(jsonBytes, token);
        }

        private static readonly JsonSerializerOptions SnakeCaseOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };

        public static async Task HttpReplyJson(Stream? incomingStream, object data, 
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyJson(incomingStream, data, null, SnakeCaseOptions, httpLoggersProvider, token);
        }

        public static async Task HttpReplyJson(SessionContext context, object data, JsonSerializerOptions serializerOptions)
        {
            await HttpReplyJson(context.IncomingStream, data, null, serializerOptions, context, context.Token);
        }

        public static async Task HttpReplyJson(SessionContext context, object data)
        {
            await HttpReplyJson(context.IncomingStream, data, null, SnakeCaseOptions, context, context.Token);
        }

        public static async Task HttpReplyJson(Stream? incomingStream, object data, string[]? customHeaders,
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            await HttpReplyJson(incomingStream, data, customHeaders, SnakeCaseOptions, httpLoggersProvider, token);
        }

        public static async Task HttpReplyJson(SessionContext context, object data, string[]? customHeaders, 
            JsonSerializerOptions serializerOptions)
        {
            await HttpReplyJson(context.IncomingStream, data, customHeaders, serializerOptions, context, context.Token);
        }

        public static async Task HttpReplyJson(SessionContext context, object data, string[]? customHeaders)
        {
            await HttpReplyJson(context.IncomingStream, data, customHeaders, SnakeCaseOptions, context, context.Token);
        }

        public static async Task HttpReplyFileStream(Stream? incomingStream, FileStream fileStream,
            IHttpLoggersProvider? httpLoggersProvider, CancellationToken token)
        {
            if (incomingStream == null) return;

            var fileInfo = new FileInfo(fileStream.Name);
            string contentType = GetContentType(fileInfo.Extension);

            string[] headers = [
                "HTTP/1.1 200 OK",
                $"Date: {DateTime.UtcNow:R}",
                "Connection: close",
                $"Content-Length: {fileInfo.Length}",
                $"Content-Type: {contentType}"
            ];

            httpLoggersProvider?.ResponseHeadersLogger?.OnHttpHeader(httpLoggersProvider, headers);

            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);

            await incomingStream.WriteAsync(headerBytes, token);
            await fileStream.CopyToAsync(incomingStream, token);
        }

        public static async Task HttpReplyFileStream(SessionContext context, FileStream fileStream)
        {
            await HttpReplyFileStream(context.IncomingStream, fileStream, context, context.Token);
        }

        public static async Task HttpReplyCacheFileStream(SessionContext context, CountingStream? incomingStream,
            CacheEntry cacheEntry, FileStream fileStream)
        {
            if (incomingStream == null) return;
            var oldCached = context.CachedReply;
            context.CachedReply = true;
            try
            {
                var headerLength = cacheEntry.HeaderLength;
                if (headerLength > 0)
                {
                    IHttpLoggersProvider httpLoggersProvider = context;

                    byte[] headerBuffer = new byte[headerLength];
                    
                    await fileStream.ReadExactlyAsync(headerBuffer, 0, headerLength, context.Token);
                    
                    string headerString = Encoding.UTF8.GetString(headerBuffer);
                    string[] headers = headerString.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);

                    httpLoggersProvider.ResponseHeadersLogger?.OnHttpHeader(httpLoggersProvider, headers);

                    await incomingStream.WriteAsync(headerBuffer.AsMemory(0, headerLength), context.Token);
                }
                await fileStream.CopyToAsync(incomingStream, context.Token);
            }
            finally
            {
                context.CachedReply = oldCached;
            }
        }

        public static async Task HttpReplyCacheFileStream(SessionContext context, CacheEntry cacheEntry, FileStream fileStream)
        {
            await HttpReplyCacheFileStream(context, context.IncomingStream, cacheEntry, fileStream);
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
