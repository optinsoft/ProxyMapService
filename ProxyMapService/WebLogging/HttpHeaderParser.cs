using ProxyMapService.Proxy.Http;
using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public static class HttpHeaderParser
    {
        public static HttpRequestDto? ParseRequestRawHeaders(string[]? rawHeaders, string id, bool completed)
        {
            if (rawHeaders == null) return null;

            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool firstLine = true;
            string? requestLine = null;
            string? httpVerb = null;
            string? httpTarget = null;

            foreach (var headerLine in rawHeaders)
            {
                if (string.IsNullOrWhiteSpace(headerLine)) break;

                if (firstLine)
                {
                    firstLine = false;
                    requestLine = headerLine;
                    var methodLength = HttpParser.StartsWithHttpMethod(headerLine.AsSpan(), false);
                    if (methodLength > 0)
                    {
                        var method = headerLine.AsSpan(0, methodLength).TrimEnd();
                        var target = headerLine.AsSpan(methodLength).TrimStart();
                        var targetEnd = target.IndexOf(' ');
                        if (targetEnd != -1)
                        {
                            var httpProto = target.Slice(targetEnd);
                            if (HttpParser.IsValidHttpVersion(httpProto))
                            {
                                target = target.Slice(0, targetEnd);
                                httpVerb = method.ToString();
                                httpTarget = target.ToString();
                            }
                        }
                    }
                    continue;
                }

                int separatorIndex = headerLine.IndexOf(':');
                if (separatorIndex > 0)
                {
                    string key = headerLine.Substring(0, separatorIndex).Trim();
                    string value = headerLine.Substring(separatorIndex + 1).Trim();

                    if (dictionary.ContainsKey(key))
                        dictionary[key] += $", {value}";
                    else
                        dictionary[key] = value;
                }
            }

            var dto = new HttpRequestDto()
            {
                Id = id,
                Completed = completed,
                RequestURI = httpTarget,
                RequestMethod = httpVerb,
                RequestLine = requestLine,
                Headers = dictionary
            };

            return dto;
        }

        public static HttpResponseDto? ParseResponseRawHeaders(string[]? rawHeaders, string id, bool completed)
        {
            if (rawHeaders == null) return null;

            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool firstLine = true;
            string? statusLine = null;
            string? httpStatusCode = null;
            string? httpStatuseText = null;
            string? contentType = null;
            string? contentLength = null;

            foreach (var headerLine in rawHeaders)
            {
                if (string.IsNullOrWhiteSpace(headerLine)) break;

                if (firstLine)
                {
                    firstLine = false;
                    statusLine = headerLine;
                    var versionLength = HttpParser.StartsWithHttpVersion(headerLine.AsSpan(), false);
                    if (versionLength > 0)
                    {
                        var version = headerLine.AsSpan(0, versionLength);
                        var status = headerLine.AsSpan(versionLength).TrimStart();
                        var codeEnd = status.IndexOf(' ');
                        if (codeEnd != -1)
                        {
                            var statusCode = status.Slice(0, codeEnd);
                            var statusText = status.Slice(codeEnd + 1);
                            httpStatusCode = statusCode.ToString();
                            httpStatuseText = statusText.ToString();
                        }
                    }
                    continue;
                }

                int separatorIndex = headerLine.IndexOf(':');
                if (separatorIndex > 0)
                {
                    string key = headerLine.Substring(0, separatorIndex).Trim();
                    string value = headerLine.Substring(separatorIndex + 1).Trim();

                    if (dictionary.ContainsKey(key))
                        dictionary[key] += $", {value}";
                    else
                        dictionary[key] = value;

                    if (key.Equals("content-type", StringComparison.OrdinalIgnoreCase)) {
                        contentType = value;
                    }
                    else if (key.Equals("content-length", StringComparison.OrdinalIgnoreCase))
                    {
                        contentLength = value;
                    }
                }
            }

            var type = contentType != null ? MimeTypeProvider.GetNetworkType(contentType) : null;
            long? size = contentLength != null && long.TryParse(contentLength, out var length) ? length : null;

            var dto = new HttpResponseDto()
            {
                Id = id,
                Completed = completed,
                StatusCode = httpStatusCode,
                StatusText = httpStatuseText,
                StatusLine = statusLine,
                Type = type,
                Size = size,
                Headers = dictionary
            };

            return dto;
        }
    }
}
