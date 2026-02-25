using Newtonsoft.Json.Linq;
using System.Text;

namespace ProxyMapService.Proxy.Http
{
    public class HttpHostRewriter
    {
        private static readonly string[] HttpMethods =
        {
            "GET", "POST", "PUT", "DELETE",
            "HEAD", "OPTIONS", "PATCH", "TRACE", "CONNECT"
        };

        private static readonly byte[][] HttpMethodPrefixBytes =
            HttpMethods.Select(m => Encoding.ASCII.GetBytes(m + " ")).ToArray();

        private static bool StartsWithHttpMethod(ReadOnlySpan<byte> span, bool partially)
        {
            foreach (var method in HttpMethodPrefixBytes)
            {
                var methodSpan = method.AsSpan();

                int compareLength = partially ? Math.Min(span.Length, methodSpan.Length) : methodSpan.Length;

                if (span.Slice(0, compareLength)
                        .SequenceEqual(methodSpan.Slice(0, compareLength)))
                {
                    return true;
                }
            }

            return false;
        }

        public static int FindHeadersEnd(MemoryStream ms, ref int searchStart)
        {
            var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

            if (searchStart < 0)
                return -1; // Searching was terminated

            // Validate the beginning of the HTTP request
            if (!StartsWithHttpMethod(span, true))
            {
                // ATTENTION!!! Terminate searching
                searchStart = -1;
                return -1;
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

        public static Memory<byte>? OverrideHostHeader(MemoryStream ms, int headersEnd, string hostname, int port)
        {
            var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

            // Validate the end of the HTTP headers
            if (headersEnd < 0)
                return null;
            if (!span.Slice(headersEnd, 4).SequenceEqual("\r\n\r\n"u8))
                return null;

            // Validate the beginning of the HTTP request
            if (!StartsWithHttpMethod(span, false))
                return null;

            int headerSectionLength = headersEnd + 4; // include \r\n\r\n

            var headerBytes = span.Slice(0, headerSectionLength);
            var bodyBytes = span.Slice(headerSectionLength);

            string headers = Encoding.ASCII.GetString(headerBytes);
            /*
            // Validate first line is HTTP request
            int firstLineEnd = headers.IndexOf("\r\n", StringComparison.Ordinal);
            if (firstLineEnd <= 0)
                return null;

            string firstLine = headers.Substring(0, firstLineEnd);

            bool isHttp = HttpMethods.Any(m =>
                firstLine.StartsWith(m + " ", StringComparison.OrdinalIgnoreCase));

            if (!isHttp)
                return null;
            */
            // Split header lines
            var lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None);

            bool hostFound = false;

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"Host: {hostname}:{port}";
                    hostFound = true;
                    break;
                }
            }

            if (!hostFound)
                return null;

            string modifiedHeaders = string.Join("\r\n", lines);

            byte[] modifiedHeaderBytes = Encoding.ASCII.GetBytes(modifiedHeaders);

            // Allocate final buffer (headers + original body)
            byte[] result = new byte[modifiedHeaderBytes.Length + bodyBytes.Length];

            Buffer.BlockCopy(modifiedHeaderBytes, 0, result, 0, modifiedHeaderBytes.Length);
            bodyBytes.CopyTo(result.AsSpan(modifiedHeaderBytes.Length));

            return new Memory<byte>(result);
        }
    }
}
