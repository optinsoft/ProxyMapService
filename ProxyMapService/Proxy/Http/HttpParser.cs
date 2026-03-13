using ProxyMapService.Proxy.Sessions;
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

        private static readonly string[] HttpVersions =
        {
            "HTTP/1.0",
            "HTTP/1.1",
            "HTTP/2"
        };

        private static readonly byte[][] HttpVersionPrefixBytes =
            HttpVersions.Select(m => Encoding.ASCII.GetBytes(m + " ")).ToArray();

        public static bool StartsWithHttpMethod(ReadOnlySpan<byte> span, bool partially)
        {
            foreach (var method in HttpMethodPrefixBytes)
            {
                var methodSpan = method.AsSpan();

                int compareLength = partially ? Math.Min(span.Length, methodSpan.Length) : methodSpan.Length;

                if (compareLength <= span.Length && span.Slice(0, compareLength)
                        .SequenceEqual(methodSpan.Slice(0, compareLength)))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool StartsWithHttpVersion(ReadOnlySpan<byte> span, bool partially)
        {
            foreach (var version in HttpVersionPrefixBytes)
            {
                var versionSpan = version.AsSpan();

                int compareLength = partially ? Math.Min(span.Length, versionSpan.Length) : versionSpan.Length;

                if (compareLength <= span.Length && span.Slice(0, compareLength)
                        .SequenceEqual(versionSpan.Slice(0, compareLength)))
                {
                    return true;
                }
            }

            return false;
        }

        public static int FindHeadersEnd(MemoryStream ms, bool response, ref int searchStart)
        {
            var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

            if (searchStart < 0)
                return -1; // Searching was terminated

            if (response)
            {
                // Validate the beginning of the HTTP response
                if (!StartsWithHttpVersion(span, true))
                {
                    // ATTENTION!!! Terminate searching
                    searchStart = -1;
                    return -1;
                }
            }
            else
            {
                // Validate the beginning of the HTTP request
                if (!StartsWithHttpMethod(span, true))
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
                if (!StartsWithHttpVersion(span, false))
                    return null;
            }
            else
            {
                // Validate the beginning of the HTTP request
                if (!StartsWithHttpMethod(span, false))
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
                if (!StartsWithHttpVersion(span, false))
                    return null;
            }
            else
            {
                // Validate the beginning of the HTTP request
                if (!StartsWithHttpMethod(span, false))
                    return null;
            }

            int headerSectionLength = headersEnd + 4; // include \r\n\r\n

            var headerSpan = span.Slice(0, headerSectionLength);
            var body = span.Slice(headerSectionLength);

            string headers = Encoding.ASCII.GetString(headerSpan);

            var lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None);

            return new HttpHeaderLinesAndBody
            {
                headerLines = lines,
                bodyBytes = body.ToArray(),
            };
        }
    }
}
