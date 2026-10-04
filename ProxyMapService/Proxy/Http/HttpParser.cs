using System.Text;

namespace ProxyMapService.Proxy.Http
{
    public class HttpParser
    {
        private static readonly string[] HttpMethods =
        {
            "GET", "POST", "PUT", "DELETE",
            "HEAD", "OPTIONS", "PATCH", "TRACE", "CONNECT"
        };

        private static readonly byte[][] HttpMethodPrefixBytes =
            HttpMethods.Select(m => Encoding.ASCII.GetBytes(m + " ")).ToArray();

        private static readonly string[] HttpMethodPrefixStrings =
            HttpMethods.Select(m => m + " ").ToArray();

        private static readonly string[] HttpVersions =
        {
            "HTTP/1.0",
            "HTTP/1.1"
        };

        private static readonly byte[][] HttpVersionPrefixBytes =
            HttpVersions.Select(m => Encoding.ASCII.GetBytes(m + " ")).ToArray();

        private static readonly string[] HttpVersionPrefixStrings =
            HttpVersions.Select(m => m + " ").ToArray();

        public static bool IsValidHttpMethod(ReadOnlySpan<char> methodSpan)
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

        public static bool IsValidHttpVersion(ReadOnlySpan<char> versionSpan)
        {
            return versionSpan.Trim() switch
            {
                "HTTP/1.0" => true,
                "HTTP/1.1" => true,
                "HTTP/2" => true,
                _ => false
            };
        }

        public static int StartsWithHttpMethod(ReadOnlySpan<byte> lineSpan, bool partially)
        {
            foreach (var method in HttpMethodPrefixBytes)
            {
                var methodSpan = method.AsSpan();
                int compareLength = partially ? Math.Min(lineSpan.Length, methodSpan.Length) : methodSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength)
                        .SequenceEqual(methodSpan.Slice(0, compareLength)))
                {
                    return compareLength;
                }
            }

            return 0;
        }

        public static int StartsWithHttpMethod(ReadOnlySpan<char> lineSpan, bool partially)
        {
            foreach (var method in HttpMethodPrefixStrings)
            {
                var methodSpan = method.AsSpan();
                int compareLength = partially ? Math.Min(lineSpan.Length, methodSpan.Length) : methodSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength)
                        .SequenceEqual(methodSpan.Slice(0, compareLength)))
                {
                    return compareLength;
                }
            }
            return 0;
        }

        public static int StartsWithHttpVersion(ReadOnlySpan<byte> lineSpan, bool partially)
        {
            foreach (var version in HttpVersionPrefixBytes)
            {
                var versionSpan = version.AsSpan();
                int compareLength = partially ? Math.Min(lineSpan.Length, versionSpan.Length) : versionSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength)
                        .SequenceEqual(versionSpan.Slice(0, compareLength)))
                {
                    return compareLength;
                }
            }

            return 0;
        }

        public static int StartsWithHttpVersion(ReadOnlySpan<char> lineSpan, bool partially)
        {
            foreach (var version in HttpVersionPrefixStrings)
            {
                var versionSpan = version.AsSpan();
                int compareLength = partially ? Math.Min(lineSpan.Length, versionSpan.Length) : versionSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength)
                        .SequenceEqual(versionSpan.Slice(0, compareLength)))
                {
                    return compareLength;
                }
            }
            return 0;
        }

        public static int FindHeadersEnd(MemoryStream ms, bool response, ref int searchStart)
        {
            var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

            if (searchStart < 0)
                return -1; // Searching was terminated

            if (response)
            {
                // Validate the beginning of the HTTP response
                if (StartsWithHttpVersion(span, true) <= 0)
                {
                    // ATTENTION!!! Terminate searching
                    searchStart = -1;
                    return -1;
                }
            }
            else
            {
                // Validate the beginning of the HTTP request
                if (StartsWithHttpMethod(span, true) <= 0)
                {
                    // ATTENTION!!! Terminate searching
                    searchStart = -1;
                    return -1;
                }
            }

            int index = span.Slice(searchStart).IndexOf("\r\n\r\n"u8);
            if (index >= 0)
            {
                index += searchStart;
            }
            else
            {
                searchStart = Math.Max(0, span.Length - 3);
            }

            return index;
        }

        public static int FindRequestHeadersEnd(MemoryStream ms, ref int searchStart)
        {
            return FindHeadersEnd(ms, false, ref searchStart);
        }

        public static int FindResponseHeadersEnd(MemoryStream ms, ref int searchStart)
        {
            return FindHeadersEnd(ms, true, ref searchStart);
        }

        public static byte[]? GetHeaderBytes(MemoryStream ms, bool response, int headersEnd)
        {
            var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

            // Validate the end of the HTTP headers
            if (headersEnd < 0)
                return null;
            if (span.Length < headersEnd + 4)
                return null;
            if (!span.Slice(headersEnd, 4).SequenceEqual("\r\n\r\n"u8))
                return null;

            if (response)
            {
                // Validate the beginning of the HTTP response
                if (StartsWithHttpVersion(span, false) <= 0)
                    return null;
            }
            else
            {
                // Validate the beginning of the HTTP request
                if (StartsWithHttpMethod(span, false) <= 0)
                    return null;
            }

            int headerSectionLength = headersEnd + 4; // include \r\n\r\n

            return span.Slice(0, headerSectionLength).ToArray();
        }

        public static byte[]? GetRequestHeaderBytes(MemoryStream ms, int headersEnd)
        {
            return GetHeaderBytes(ms, false, headersEnd);
        }

        public static byte[]? GetResponseHeaderBytes(MemoryStream ms, int headersEnd)
        {
            return GetHeaderBytes(ms, true, headersEnd);
        }

        public static HttpHeaderLinesAndBody? GetHeaderLinesAndBody(MemoryStream ms, bool response, int headersEnd)
        {
            var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

            // Validate the end of the HTTP headers
            if (headersEnd < 0)
                return null;
            if (span.Length < headersEnd + 4)
                return null;
            if (!span.Slice(headersEnd, 4).SequenceEqual("\r\n\r\n"u8))
                return null;

            if (response)
            {
                // Validate the beginning of the HTTP response
                if (StartsWithHttpVersion(span, false) <= 0)
                    return null;
            }
            else
            {
                // Validate the beginning of the HTTP request
                if (StartsWithHttpMethod(span, false) <= 0)
                    return null;
            }

            int headerSectionLength = headersEnd + 4; // include \r\n\r\n

            var headerSpan = span.Slice(0, headerSectionLength);
            var body = span.Slice(headerSectionLength);

            string header = Encoding.ASCII.GetString(headerSpan);

            var lines = header.Split(["\r\n"], StringSplitOptions.None);

            return new HttpHeaderLinesAndBody
            {
                HeaderLines = lines,
                BodyBytes = body.ToArray(),
            };
        }

        public static HttpHeaderLinesAndBody? GetRequestHeaderLinesAndBody(MemoryStream ms, int headersEnd)
        {
            return GetHeaderLinesAndBody(ms, false, headersEnd);
        }

        public static HttpHeaderLinesAndBody? GetResponseHeaderLinesAndBody(MemoryStream ms, int headersEnd)
        {
            return GetHeaderLinesAndBody(ms, true, headersEnd);
        }
    }
}
