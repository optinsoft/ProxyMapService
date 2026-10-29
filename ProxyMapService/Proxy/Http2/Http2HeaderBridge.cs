using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Http2
{
    /// <summary>Builds regular h1 headers from h2 headers (for logging only; they are never sent over the wire).</summary>
    internal static class Http2HeaderBridge
    {
        public static HttpRequestHeader CreateRequestHeader(IReadOnlyList<H2Header> headers)
        {
            string method = "", path = "/";
            string? authority = null;
            bool hasHost = false;
            List<string>? cookies = null;
            var lines = new List<string>(headers.Count + 2) { "" }; // [0] — the start line; we'll fill it in at the end

            foreach (var (name, value) in headers)
            {
                switch (name)
                {
                    case ":method": method = Clean(value); continue;
                    case ":path": path = Clean(value); continue;
                    case ":authority": authority = Clean(value); continue;
                    case "cookie":
                        cookies ??= new List<string>();
                        cookies.Add(Clean(value)); // in h2, cookies may arrive in multiple header fields
                        continue;
                    case "host": hasHost = true; break;
                }
                if (name.StartsWith(':')) continue; // :scheme and other pseudo-headers
                lines.Add($"{Clean(name)}: {Clean(value)}");
            }

            if (cookies != null) lines.Add("cookie: " + string.Join("; ", cookies));
            if (authority != null && !hasHost) lines.Insert(1, "host: " + authority);
            lines[0] = $"{method} {path} HTTP/2";
            return new HttpRequestHeader(lines.ToArray());
        }

        public static HttpResponseHeader CreateResponseHeader(IReadOnlyList<H2Header> headers, int status)
        {
            var lines = new List<string>(headers.Count + 1) { "" };
            foreach (var (name, value) in headers)
            {
                if (name.StartsWith(':')) continue;
                lines.Add($"{Clean(name)}: {Clean(value)}");
            }
            lines[0] = $"HTTP/2 {status} {Http1Http2Converter.Reason(status)}";

            int length = 2; // trailing CRLF
            foreach (var l in lines) length += l.Length + 2;
            return new HttpResponseHeader(lines.ToArray(), length);
        }

        // protection of logs against line injection: CR/LF are not allowed in values in h2, but a peer may still send them
        private static string Clean(string s)
            => s.AsSpan().IndexOfAny('\r', '\n') < 0 ? s : s.Replace('\r', ' ').Replace('\n', ' ');
    }

}
