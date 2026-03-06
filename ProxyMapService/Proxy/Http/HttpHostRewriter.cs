using Newtonsoft.Json.Linq;
using System.Text;

namespace ProxyMapService.Proxy.Http
{
    public class HttpHostRewriter
    {
        public static byte[]? OverrideHostHeader(byte[] buffer, int bufferLength, int headersEnd, string hostname, int port)
        {
            var span = buffer.AsSpan(0, bufferLength);

            // Validate the end of the HTTP headers
            if (headersEnd < 0)
                return null;
            if (!span.Slice(headersEnd, 4).SequenceEqual("\r\n\r\n"u8))
                return null;

            // Validate the beginning of the HTTP request
            if (!HttpParser.StartsWithHttpMethod(span, false))
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

            return result;
        }
    }
}
