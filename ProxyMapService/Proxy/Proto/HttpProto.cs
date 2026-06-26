using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using System.Text;
using System.Text.Json;

namespace ProxyMapService.Proxy.Proto
{
    public class HttpProto
    {
        public static async Task HttpReplyConnectionEstablished(SessionContext context, Stream? incomingStream)
        {
            if (incomingStream == null) return;
            string[] headers = [
                "HTTP/1.1 200 Connection established",
                $"Date: {DateTime.UtcNow:R}"
            ];
            IHttpLoggersProvider httpLoggersProvider = context;
            httpLoggersProvider.ResponseHeadersLogger?.OnHttpHeader(context, headers);
            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var bytes = Encoding.ASCII.GetBytes(headerText);
            await incomingStream.WriteAsync(bytes, context.Token);
        }

        public static async Task HttpReplyConnectionEstablished(SessionContext context)
        {
            await HttpReplyConnectionEstablished(context, context.IncomingStream);
        }

        public static async Task HttpReplyError(SessionContext context, Stream? incomingStream, string httpStatusLine, List<string>? customHeaders, string? errorMessage)
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
            string contentType = "text/plain; charset=utf-8";
            if (contentBytes.Length > 0)
            {
                headers.Add($"Content-Type: {contentType}");
            }
            headers.Add($"Content-Length: {contentBytes.Length}");
            IHttpLoggersProvider httpLoggersProvider = context;
            httpLoggersProvider.ResponseHeadersLogger?.OnHttpHeader(context, [.. headers]);
            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);
            await incomingStream.WriteAsync(headerBytes, context.Token);
            if (contentBytes.Length > 0)
            {
                httpLoggersProvider.ResponseBodyLogger?.OnCompleted(context, contentType, contentBytes.Length, contentBytes);
                await incomingStream.WriteAsync(contentBytes, context.Token);
            }
        }

        public static async Task HttpReplyError(SessionContext context, Stream? incomingStream, string httpStatusLine, List<string>? customHeaders)
        {
            await HttpReplyError(context, incomingStream, httpStatusLine, customHeaders, null);
        }

        public static async Task HttpReplyError(SessionContext context, Stream? incomingStream, string httpStatusLine, string? errorMessage)
        {
            await HttpReplyError(context, incomingStream, httpStatusLine, null, errorMessage);
        }

        public static async Task HttpReplyError(SessionContext context, Stream? incomingStream, string httpStatusLine)
        {
            await HttpReplyError(context, incomingStream, httpStatusLine, null, null);
        }

        public static async Task HttpReplyError(SessionContext context, string httpStatusLine, string? errorMessage = null)
        {
            await HttpReplyError(context, context.IncomingStream, httpStatusLine, null, errorMessage);
        }

        public static async Task HttpReplyBadGateway(SessionContext context, Stream? incomingStream, string? errorMessage)
        {
            await HttpReplyError(context, incomingStream, "HTTP/1.1 502 Bad Gateway", null, errorMessage);
        }

        public static async Task HttpReplyBadGateway(SessionContext context, string? errorMessage = null)
        {
            await HttpReplyBadGateway(context, context.IncomingStream, errorMessage);
        }

        public static async Task HttpReplyGatewayTimeout(SessionContext context, Stream? incomingStream, string? errorMessage)
        {
            await HttpReplyError(context, incomingStream, "HTTP/1.1 504 Gateway Timeout", null, errorMessage);
        }

        public static async Task HttpReplyGatewayTimeout(SessionContext context, string? errorMessage)
        {
            await HttpReplyGatewayTimeout(context, context.IncomingStream, errorMessage);
        }

        public static async Task HttpReplyProxyAuthenticationRequired(SessionContext context, Stream? incomingStream)
        {
            await HttpReplyError(context, incomingStream, "HTTP/1.1 407 Proxy Authentication Required", 
                ["Proxy-Authenticate: Basic realm=\"Pass Through Proxy\""]);
        }

        public static async Task HttpReplyProxyAuthenticationRequired(SessionContext context)
        {
            await HttpReplyProxyAuthenticationRequired(context, context.IncomingStream);
        }

        public static async Task HttpReplyProxyUnauthorized(SessionContext context, Stream? incomingStream)
        {
            await HttpReplyError(context, incomingStream, "HTTP/1.1 401 Unauthorized", 
                ["Proxy-Authenticate: Basic realm=\"Pass Through Proxy\""]);
        }

        public static async Task HttpReplyProxyUnauthorized(SessionContext context)
        {
            await HttpReplyProxyUnauthorized(context, context.IncomingStream);
        }

        public static async Task HttpReplyBadRequest(SessionContext context, Stream? incomingStream)
        {
            await HttpReplyError(context, incomingStream, "HTTP/1.1 400 Bad Request");
        }

        public static async Task HttpReplyBadRequest(SessionContext context)
        {
            await HttpReplyBadRequest(context, context.IncomingStream);
        }

        public static async Task HttpReplyForbidden(SessionContext context, Stream? incomingStream)
        {
            await HttpReplyError(context, incomingStream, "HTTP/1.1 403 Forbidden");
        }

        public static async Task HttpReplyForbidden(SessionContext context)
        {
            await HttpReplyForbidden(context, context.IncomingStream);
        }

        public static async Task HttpReplyNotFound(SessionContext context, Stream? incomingStream)
        {
            await HttpReplyError(context, incomingStream, "HTTP/1.1 404 Not Found");
        }

        public static async Task HttpReplyNotFound(SessionContext context)
        {
            await HttpReplyNotFound(context, context.IncomingStream);
        }

        public static async Task HttpReplyMethodNotAllowed(SessionContext context, Stream? incomingStream)
        {
            await HttpReplyError(context, incomingStream, "HTTP/1.1 405 Method Not Allowed");
        }
        
        public static async Task HttpReplyMethodNotAllowed(SessionContext context)
        {
            await HttpReplyMethodNotAllowed(context, context.IncomingStream);
        }

        public static async Task HttpReplyText(SessionContext context, Stream? incomingStream, string text)
        {
            if (incomingStream == null) return;

            byte[] textBytes = Encoding.UTF8.GetBytes(text);

            string contentType = "text/plain; charset=utf-8";

            string[] headers = [
                "HTTP/1.1 200 OK",
                $"Date: {DateTime.UtcNow:R}",
                "Connection: close",
                $"Content-Length: {textBytes.Length}",
                $"Content-Type: {contentType}"
            ];

            IHttpLoggersProvider httpLoggersProvider = context;
            httpLoggersProvider.ResponseHeadersLogger?.OnHttpHeader(context, headers);

            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);

            await incomingStream.WriteAsync(headerBytes, context.Token);

            if (textBytes.Length > 0)
            {
                httpLoggersProvider.ResponseBodyLogger?.OnCompleted(context, contentType, textBytes.Length, textBytes);
                await incomingStream.WriteAsync(textBytes, context.Token);
            }
        }

        public static async Task HttpReplyJson(SessionContext context, Stream? incomingStream, object data, string[]? customHeaders, 
            JsonSerializerOptions serializerOptions)
        {
            if (incomingStream == null) return;

            byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(data, serializerOptions);

            string contentType = "application/json; charset=utf-8";

            string[] headers = [
                "HTTP/1.1 200 OK",
                $"Date: {DateTime.UtcNow:R}",
                "Connection: close",
                $"Content-Length: {jsonBytes.Length}",
                $"Content-Type: {contentType}",
                .. (customHeaders ?? [])
            ];

            IHttpLoggersProvider httpLoggersProvider = context;
            httpLoggersProvider.ResponseHeadersLogger?.OnHttpHeader(context, headers);

            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);

            await incomingStream.WriteAsync(headerBytes, context.Token);

            if (jsonBytes.Length > 0)
            {
                httpLoggersProvider.ResponseBodyLogger?.OnCompleted(context, contentType, jsonBytes.Length, jsonBytes);
                await incomingStream.WriteAsync(jsonBytes, context.Token);
            }
        }

        private static readonly JsonSerializerOptions SnakeCaseOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };

        public static async Task HttpReplyJson(SessionContext context, Stream? incomingStream, object data)
        {
            await HttpReplyJson(context, incomingStream, data, null, SnakeCaseOptions);
        }

        public static async Task HttpReplyJson(SessionContext context, object data, JsonSerializerOptions serializerOptions)
        {
            await HttpReplyJson(context, context.IncomingStream, data, null, serializerOptions);
        }

        public static async Task HttpReplyJson(SessionContext context, object data)
        {
            await HttpReplyJson(context, context.IncomingStream, data, null, SnakeCaseOptions);
        }

        public static async Task HttpReplyJson(SessionContext context, Stream? incomingStream, object data, string[]? customHeaders)
        {
            await HttpReplyJson(context, incomingStream, data, customHeaders, SnakeCaseOptions);
        }

        public static async Task HttpReplyJson(SessionContext context, object data, string[]? customHeaders, 
            JsonSerializerOptions serializerOptions)
        {
            await HttpReplyJson(context, context.IncomingStream, data, customHeaders, serializerOptions);
        }

        public static async Task HttpReplyJson(SessionContext context, object data, string[]? customHeaders)
        {
            await HttpReplyJson(context, context.IncomingStream, data, customHeaders, SnakeCaseOptions);
        }

        public static async Task HttpReplyFileStream(SessionContext context, Stream? incomingStream, FileStream fileStream)
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

            IHttpLoggersProvider httpLoggersProvider = context;

            httpLoggersProvider.ResponseHeadersLogger?.OnHttpHeader(context, headers);

            var headerText = string.Join("\r\n", [.. headers, "\r\n"]);
            var headerBytes = Encoding.ASCII.GetBytes(headerText);

            await incomingStream.WriteAsync(headerBytes, context.Token);

            var bodyLength = fileStream.Length;
            if (bodyLength > 0)
            {
                var bodyLogger = httpLoggersProvider.ResponseBodyLogger;
                if (bodyLogger != null)
                {
                    using MemoryStream bodyStream = new();
                    fileStream.CopyTo(bodyStream);
                    bodyStream.Position = 0;
                    bodyLogger.OnCompleted(context, contentType, bodyLength, bodyStream);
                    bodyStream.Position = 0;
                    await bodyStream.CopyToAsync(incomingStream, context.Token);
                }
                else
                {
                    await fileStream.CopyToAsync(incomingStream, context.Token);
                }
            }
        }

        public static async Task HttpReplyFileStream(SessionContext context, FileStream fileStream)
        {
            await HttpReplyFileStream(context, context.IncomingStream, fileStream);
        }

        public static async Task HttpReplyCacheFileStream(SessionContext context, CountingStream? incomingStream,
            CacheEntry cacheEntry, FileStream fileStream)
        {
            if (incomingStream == null) return;
            var oldCached = context.CachedReply;
            context.CachedReply = true;
            try
            {
                IHttpLoggersProvider httpLoggersProvider = context;

                var headerLength = cacheEntry.HeaderLength;
                if (headerLength > 0)
                {
                    byte[] headerBuffer = new byte[headerLength];

                    await fileStream.ReadExactlyAsync(headerBuffer, 0, headerLength, context.Token);

                    string headerString = Encoding.UTF8.GetString(headerBuffer);
                    string[] headers = headerString.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);

                    httpLoggersProvider.ResponseHeadersLogger?.OnHttpHeader(context, headers);

                    await incomingStream.WriteAsync(headerBuffer.AsMemory(0, headerLength), context.Token);
                }

                var bodyLength = fileStream.Length - fileStream.Position;
                if (bodyLength > 0)
                {
                    var bodyLogger = httpLoggersProvider.ResponseBodyLogger;
                    if (bodyLogger != null)
                    {
                        using MemoryStream bodyStream = new();
                        fileStream.CopyTo(bodyStream);
                        bodyStream.Position = 0;
                        bodyLogger.OnCompleted(context, cacheEntry.ContentType, bodyLength, bodyStream);
                        bodyStream.Position = 0;
                        await bodyStream.CopyToAsync(incomingStream, context.Token);
                    }
                    else
                    {
                        await fileStream.CopyToAsync(incomingStream, context.Token);
                    }
                }
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
