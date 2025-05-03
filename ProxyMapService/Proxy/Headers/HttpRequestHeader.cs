using System.Text;
using Microsoft.Extensions.Primitives;
using ProxyMapService.Proxy.Network;

namespace ProxyMapService.Proxy.Headers
{
    public class HttpRequestHeader
    {
        public HttpRequestHeader(byte[] array)
        {
            Parse(this, array);
        }

        public Address? Host { get; private set; }
        public long? ContentLength { get; private set; }
        public string? HTTPVerb { get; private set; }
        public string? HTTPTarget { get; private set; }
        public string? HTTPProtocol { get; private set; }
        public string? ProxyAuthorization { get; private set; }
        public IEnumerable<string>? ArrayList { get; private set; }

        public byte[] GetBytes(bool keepProxyHeaders, string? customProxyAuthorization, string? customFirstLine)
        {
            return GetBytes(ArrayList, keepProxyHeaders, customProxyAuthorization, customFirstLine);
        }

        public string? GetHTTPTargetPath()
        {
            return GetHTTPTargetPath(HTTPTarget);
        }

        private static void Parse(HttpRequestHeader self, byte[] array)
        {
            var strings = Encoding.ASCII.GetString(array).Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);

            self.Host = GetAddress(strings);
            self.ContentLength = GetContentLength(strings);
            self.HTTPVerb = GetHTTPVerb(strings);
            self.HTTPTarget = GetHTTPTarget(strings);
            self.HTTPProtocol = GetHTTPProtocol(strings);
            self.ProxyAuthorization = GetProxyAuthorization(strings);
            self.ArrayList = strings;
        }

        private static byte[] GetBytes(IEnumerable<string>? arrayList, bool keepProxyHeaders, string? customProxyAuthorization, string? customFirstLine)
        {
            var builder = new StringBuilder();

            if (arrayList != null)
            {
                var enumerable = keepProxyHeaders && (customProxyAuthorization == null) 
                    ? arrayList 
                    : (keepProxyHeaders 
                        ? arrayList.Where(@string => !@string.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
                        : arrayList.Where(@string => !@string.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase)
                                                  && !@string.StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase)));
                if (customFirstLine != null)
                {
                    builder.Append(customFirstLine).Append("\r\n");
                }
                var line = 0;
                foreach (var @string in enumerable)
                {
                    if (line > 0 || customFirstLine == null)
                    {
                        builder.Append(@string).Append("\r\n");
                    }
                    line += 1;
                }

                if (keepProxyHeaders && (customProxyAuthorization != null))
                {
                    builder.Append($"Proxy-Authorization: Basic {customProxyAuthorization}\r\n");
                }

                builder.Append("\r\n");
            }

            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private static Address GetAddress(IEnumerable<string> strings)
        {
            const string key = "host:";

            var split = strings
                .Single(s => s.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                .Substring(key.Length)
                .TrimStart()
                .Split(':');

            switch (split.Length)
            {
                case 1:
                    return new Address(split[0], 80);
                case 2:
                    return new Address(split[0], int.Parse(split[1]));
                default:
                    throw new FormatException(string.Join(":", split));
            }
        }

        private static long GetContentLength(IEnumerable<string> strings)
        {
            const string key = "content-length:";

            return Convert.ToInt64(strings
                .SingleOrDefault(s => s.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                ?.Substring(key.Length)
                .TrimStart());
        }

        private static string GetHTTPVerb(IEnumerable<string> strings)
        {
            return strings.First().Split(' ').First();
        }

        private static string? GetHTTPTarget(IEnumerable<string> strings)
        {
            var split = strings.First().Split(' ', 2);
            if (split.Length < 2) return null;
            return split[1].TrimStart().Split(' ').First();
        }

        private static string? GetHTTPProtocol(IEnumerable<string> strings)
        {
            var split1 = strings.First().Split(' ', 2);
            if (split1.Length < 2) return null;
            var split2 = split1[1].TrimStart().Split(' ', 2);
            if (split2.Length < 2) return null;
            return split2[1].Trim();
        }

        private static string? GetProxyAuthorization(IEnumerable<string> strings)
        {
            const string key = "Proxy-Authorization: Basic";

            return strings
                .FirstOrDefault(@string => @string.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                ?.Substring(key.Length)
                .Trim();
        }

        private static string? GetHTTPTargetPath(string? target)
        {
            if (target == null) return null;
            Uri uri = new(target);
            return uri.PathAndQuery;
        }
    }
}