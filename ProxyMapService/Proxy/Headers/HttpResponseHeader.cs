using System.Text;

namespace ProxyMapService.Proxy.Headers
{
    public class HttpResponseHeader
    {
        public HttpResponseHeader(byte[] array)
        {
            HeaderLength = array.Length;
            Parse(this, array);
        }

        public HttpResponseHeader(string[] strings, int headerLength)
        {
            HeaderLength = headerLength;
            Parse(this, strings);
        }

        public int HeaderLength { get; private set; }
        public bool BadResponse { get; private set; }
        public string? HTTPProtocol { get; private set; }
        public string? StatusCode { get; private set; }
        public string? StatusText { get; private set; }
        public long? ContentLength { get; private set; }
        public string? ContentType { get; private set; }
        public string? ETag { get; private set; }
        public string[]? Headers { get; private set; }

        private static void Parse(HttpResponseHeader self, byte[] array)
        {
            var strings = Encoding.ASCII.GetString(array).Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
            Parse(self, strings);
        }

        private static void Parse(HttpResponseHeader self, string[] strings)
        {
            self.BadResponse = false;
            self.HTTPProtocol = GetHTTPProtocol(strings);
            self.StatusCode = GetStatusCode(strings);
            self.StatusText = GetStatusText(strings);
            self.ContentLength = GetContentLength(strings);
            self.ContentType = GetHeaderValue(strings, "content-type:");
            self.ETag = GetHeaderValue(strings, "etag");
            self.Headers = strings;
        }

        private static string GetHTTPProtocol(IEnumerable<string> strings)
        {
            return strings.First().Split(' ').First();
        }

        private static string? GetStatusCode(IEnumerable<string> strings)
        {
            var split = strings.First().Split(' ', 2);
            if (split.Length < 2) return null;
            return split[1].TrimStart().Split(' ').First();
        }

        private static string? GetStatusText(IEnumerable<string> strings)
        {
            var split1 = strings.First().Split(' ', 2);
            if (split1.Length < 2) return null;
            var split2 = split1[1].TrimStart().Split(' ', 2);
            if (split2.Length < 2) return null;
            return split2[1].Trim();
        }
        
        private static long GetContentLength(IEnumerable<string> strings)
        {
            const string key = "content-length:";
            return Convert.ToInt64(strings
                .SingleOrDefault(s => s.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                ?.Substring(key.Length)
                .TrimStart());
        }

        private static string? GetHeaderValue(IEnumerable<string> strings, string key)
        {
            return strings
                .SingleOrDefault(s => s.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                ?.Substring(key.Length)
                .TrimStart();
        }
    }
}
