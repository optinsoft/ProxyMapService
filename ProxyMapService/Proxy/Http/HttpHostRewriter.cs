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

        public static Memory<byte>? OverrideHostHeader(Memory<byte> input, string hostname, int port)
        {
            ReadOnlySpan<byte> span = input.Span;

            // Find end of headers: \r\n\r\n
            int headersEnd = IndexOf(span, "\r\n\r\n"u8);
            if (headersEnd < 0)
                return null;

            int headerSectionLength = headersEnd + 4; // include \r\n\r\n

            var headerBytes = span.Slice(0, headerSectionLength);
            var bodyBytes = span.Slice(headerSectionLength);

            string headers = Encoding.ASCII.GetString(headerBytes);

            // Validate first line is HTTP request
            int firstLineEnd = headers.IndexOf("\r\n", StringComparison.Ordinal);
            if (firstLineEnd <= 0)
                return null;

            string firstLine = headers.Substring(0, firstLineEnd);

            bool isHttp = HttpMethods.Any(m =>
                firstLine.StartsWith(m + " ", StringComparison.OrdinalIgnoreCase));

            if (!isHttp)
                return null;

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

        private static int IndexOf(ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
        {
            for (int i = 0; i <= span.Length - value.Length; i++)
            {
                if (span.Slice(i, value.Length).SequenceEqual(value))
                    return i;
            }
            return -1;
        }
    }
}
