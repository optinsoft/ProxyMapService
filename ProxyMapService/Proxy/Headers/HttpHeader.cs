using System.Text;
using Microsoft.Extensions.Primitives;
using Proxy.Network;

namespace Proxy.Headers
{
    public class HttpHeader
    {
        public HttpHeader(byte[] array)
        {
            Parse(this, array);
        }

        public Address? Host { get; private set; }
        public long? ContentLength { get; private set; }
        public string? Verb { get; private set; }
        public string? ProxyAuthorization { get; private set; }
        public IEnumerable<string>? ArrayList { get; private set; }

        public byte[] GetBytes(string? customProxyAuthorization)
        {
            return GetBytes(ArrayList, customProxyAuthorization);
        }

        private static void Parse(HttpHeader self, byte[] array)
        {
            var strings = Encoding.ASCII.GetString(array).Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);

            self.Host = GetAddress(strings);
            self.ContentLength = GetContentLength(strings);
            self.Verb = GetVerb(strings);
            self.ProxyAuthorization = GetProxyAuthorization(strings);
            self.ArrayList = strings;
        }

        private static byte[] GetBytes(IEnumerable<string>? arrayList, string? customProxyAuthorization)
        {
            var builder = new StringBuilder();

            if (arrayList != null)
            {
                var enumerable = customProxyAuthorization == null ? arrayList : arrayList
                    .Where(@string => !@string.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase));

                foreach (var @string in enumerable)
                {
                    builder.Append(@string).Append("\r\n");
                }

                if (customProxyAuthorization != null)
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

        private static string GetVerb(IEnumerable<string> strings)
        {
            return strings.First().Split(' ').First();
        }

        private static string? GetProxyAuthorization(IEnumerable<string> strings)
        {
            const string key = "Proxy-Authorization: Basic";

            return strings
                .FirstOrDefault(@string => @string.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                ?.Substring(key.Length)
                .Trim();
        }
    }
}