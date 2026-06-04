using ProxyMapService.Proxy.Counters;
using System.Text;

namespace ProxyMapService.Proxy.Headers
{
    public class HttpResponseHeader
    {
        public HttpResponseHeader(byte[] array, IHttpLoggersProvider? httpLoggersProvider)
        {
            HeaderLength = array.Length;
            Parse(this, array, httpLoggersProvider);
        }

        public HttpResponseHeader(string[] strings, int headerLength, IHttpLoggersProvider? httpLoggersProvider)
        {
            HeaderLength = headerLength;
            Parse(this, strings, httpLoggersProvider);
        }

        public int HeaderLength { get; private set; }
        public bool BadResponse { get; private set; }
        public string? HTTPProtocol { get; private set; }
        public string? StatusCode { get; private set; }
        public string? StatusText { get; private set; }
        public long? ContentLength { get; private set; }
        public string? ContentType { get; private set; }
        public string? ETag { get; private set; }
        public string? CacheControl { get; private set; }
        public string? Date {  get; private set; }
        public string? Expires { get; private set; }
        public string? LastModified { get; private set; }
        public string[]? Headers { get; private set; }

        private static void Parse(HttpResponseHeader self, byte[] array, IHttpLoggersProvider? httpLoggersProvider)
        {
            var strings = Encoding.ASCII.GetString(array).Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
            Parse(self, strings, httpLoggersProvider);
        }

        private static void Parse(HttpResponseHeader self, string[] strings, IHttpLoggersProvider? httpLoggersProvider)
        {
            self.BadResponse = false;
            self.HTTPProtocol = GetHTTPProtocol(strings);
            self.StatusCode = GetStatusCode(strings);
            self.StatusText = GetStatusText(strings);
            self.ContentLength = GetContentLength(strings);
            self.ContentType = GetSingleHeaderValue(strings, "content-type:");
            self.ETag = GetFirstHeaderValue(strings, "etag:");
            self.CacheControl = GetCommaSeparatedHeaderValues(strings, "cache-control:");
            self.Date = GetSingleHeaderValue(strings, "date:");
            self.Expires = GetSingleHeaderValue(strings, "expires:", "0");
            self.LastModified = GetSingleHeaderValue(strings, "last-modified:");
            self.Headers = strings;
            httpLoggersProvider?.ResponseHeadersLogger.OnHttpHeader(httpLoggersProvider, strings);
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

        private static string? GetSingleHeaderValue(IEnumerable<string> strings, string key)
        {
            return strings
                .SingleOrDefault(s => s.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                ?.Substring(key.Length)
                .TrimStart();
        }

        private static string? GetSingleHeaderValue(IEnumerable<string> strings, string key, string? fallbackIfMultiple)
        {
            var values = strings
                .Where(s => s.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Substring(key.Length).TrimStart())
                .ToList();
            if (values.Count == 0)
            {
                return null;
            }
            if (values.Count > 1)
            {
                return fallbackIfMultiple;
            }
            return values[0];
        }

        private static string? GetFirstHeaderValue(IEnumerable<string> strings, string key)
        {
            return strings
                .FirstOrDefault(s => s.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                ?.Substring(key.Length)
                .TrimStart();
        }

        private static string? GetCommaSeparatedHeaderValues(IEnumerable<string> strings, string key)
        {
            var values = strings
                .Where(s => s.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Substring(key.Length).Trim())
                .Where(v => !string.IsNullOrEmpty(v));
            return values.Any() ? string.Join(", ", values) : null;
        }
    }
}
