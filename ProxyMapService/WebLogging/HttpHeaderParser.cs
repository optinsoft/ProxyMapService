using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public static class HttpHeaderParser
    {
        private static readonly string[] HttpMethods =
        {
            "GET", "POST", "PUT", "DELETE",
            "HEAD", "OPTIONS", "PATCH", "TRACE", "CONNECT"
        };

        private static readonly string[] HttpMethodPrefixStrings =
            HttpMethods.Select(m => m + " ").ToArray();

        private static readonly string[] HttpVersions =
        {
            "HTTP/1.0",
            "HTTP/1.1",
            "HTTP/2"
        };

        private static readonly string[] HttpVersionPrefixStrings =
            HttpVersions.Select(m => m + " ").ToArray();

        private static int StartsWithHttpMethod(ReadOnlySpan<char> lineSpan)
        {
            foreach (var method in HttpMethodPrefixStrings)
            {
                var methodSpan = method.AsSpan();
                int compareLength = methodSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength).SequenceEqual(methodSpan))
                {
                    return compareLength;
                }
            }
            return 0;
        }

        private static int StartsWithHttpVersion(ReadOnlySpan<char> lineSpan)
        {
            foreach (var method in HttpVersionPrefixStrings)
            {
                var methodSpan = method.AsSpan();
                int compareLength = methodSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength).SequenceEqual(methodSpan))
                {
                    return compareLength;
                }
            }
            return 0;
        }

        private static bool IsValidHttpMethod(ReadOnlySpan<char> methodSpan)
        {
            return methodSpan.Trim() switch
            {
                "GET" => true,
                "POST" => true,
                "PUT" => true,
                "DELETE" => true,
                "HEAD" => true,
                "OPTIONS" => true,
                "PATCH" => true,
                "TRACE" => true,
                "CONNECT" => true,
                _ => false
            };
        }

        private static bool IsValidHttpVersion(ReadOnlySpan<char> versionSpan)
        {
            return versionSpan.Trim() switch
            {
                "HTTP/1.0" => true,
                "HTTP/1.1" => true,
                "HTTP/2" => true,
                _ => false
            };
        }

        public static HttpRequestDto? ParseRequestRawHeaders(string[]? rawHeaders, string id, string route)
        {
            if (rawHeaders == null) return null;

            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool firstLine = true;
            string? httpVerb = null;
            string? httpTarget = null;

            foreach (var headerLine in rawHeaders)
            {
                if (string.IsNullOrWhiteSpace(headerLine)) break;

                if (firstLine)
                {
                    firstLine = false;
                    var methodLength = StartsWithHttpMethod(headerLine.AsSpan());
                    if (methodLength > 0)
                    {
                        var method = headerLine.AsSpan(0, methodLength).TrimEnd();
                        var target = headerLine.AsSpan(methodLength).TrimStart();
                        var targetEnd = target.IndexOf(' ');
                        if (targetEnd != -1)
                        {
                            var httpProto = target.Slice(targetEnd);
                            if (IsValidHttpVersion(httpProto))
                            {
                                target = target.Slice(0, targetEnd);
                                httpVerb = method.ToString();
                                httpTarget = target.ToString();
                                continue;
                            }
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(headerLine)) break;

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
                Method = httpVerb,
                Target = httpTarget,
                Route = route,
                Headers = dictionary
            };

            return dto;
        }

        public static HttpResponseDto? ParseResponseRawHeaders(string[]? rawHeaders, string id, string route)
        {
            if (rawHeaders == null) return null;

            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool firstLine = true;
            string? httpStatusCode = null;
            string? httpStatuseText = null;

            foreach (var headerLine in rawHeaders)
            {
                if (string.IsNullOrWhiteSpace(headerLine)) break;

                if (firstLine)
                {
                    firstLine = false;
                    var versionLength = StartsWithHttpVersion(headerLine.AsSpan());
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
                            continue;
                        }
                    }
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

            var dto = new HttpResponseDto()
            {
                Id = id,
                StatusCode = httpStatusCode,
                StatusText = httpStatuseText,
                Route = route,
                Headers = dictionary
            };

            return dto;
        }
    }
}
