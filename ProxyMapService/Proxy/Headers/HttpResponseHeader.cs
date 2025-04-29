using System.Text;

namespace ProxyMapService.Proxy.Headers
{
    public class HttpResponseHeader
    {
        public HttpResponseHeader(byte[] array)
        {
            Parse(this, array);
        }

        public string? HTTPProtocol { get; private set; }
        public string? StatusCode { get; private set; }
        public string? StatusText { get; private set; }
        public IEnumerable<string>? ArrayList { get; private set; }

        private static void Parse(HttpResponseHeader self, byte[] array)
        {
            var strings = Encoding.ASCII.GetString(array).Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);

            self.HTTPProtocol = GetHTTPProtocol(strings);
            self.StatusCode = GetStatusCode(strings);
            self.StatusText = GetStatusText(strings);

            self.ArrayList = strings;
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
    }
}
