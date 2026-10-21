using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Http2
{
    public enum BodyMode { None, ContentLength, Chunked }

    public static partial class Http1Http2Converter
    {
        private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
        {
            "connection", "keep-alive", "proxy-connection", "proxy-authenticate",
            "proxy-authorization", "transfer-encoding", "upgrade", "host"
        };

        private static readonly HashSet<string> ForbiddenTrailers = new(StringComparer.OrdinalIgnoreCase)
        {
            "content-length", "transfer-encoding", "host", "connection", "te", "trailer"
        };

        private static bool HasCtl(string s)
        {
            foreach (var c in s) if (c == '\r' || c == '\n' || c == '\0') return true;
            return false;
        }

        private static bool TrySplit(string line, out string name, out string value)
        {
            name = value = "";
            int i = line.IndexOf(':');
            if (i <= 0 || char.IsWhiteSpace(line[0]) || char.IsWhiteSpace(line[i - 1])) return false;
            name = line[..i].ToLowerInvariant();
            value = line[(i + 1)..].Trim();
            return !HasCtl(value);
        }

        // ---------------- h1 request -> h2 headers ----------------

        public static bool TryBuildRequest(HttpRequestHeader h, bool secure, out List<H2Header> result, out string? error)
        {
            result = [];
            error = null;
            if (h.BadRequest || h.HTTPVerb == null || h.HTTPTargetPath == null || h.Headers == null)
            { error = "Malformed request"; return false; }
            if (h.HTTPVerb == "CONNECT")
            { error = "CONNECT cannot be translated to HTTP/2"; return false; }

            var authority = h.Host ?? (h.HTTPTargetHost != null
                ? $"{h.HTTPTargetHost.Hostname}:{h.HTTPTargetHost.Port}" : null);
            if (string.IsNullOrEmpty(authority)) { error = "Missing Host"; return false; }

            var fields = new List<(string Name, string Value)>();
            foreach (var line in h.Headers.Skip(1))
            {
                if (line.Length == 0) continue;
                if (!TrySplit(line, out var n, out var v)) { error = "Malformed header line"; return false; }
                fields.Add((n, v));
            }

            string? cl = null;
            bool hasTe = false;
            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (n, v) in fields)
            {
                switch (n)
                {
                    case "content-length":
                        if (!ulong.TryParse(v, out _) || (cl != null && cl != v))
                        { error = "Invalid Content-Length"; return false; }
                        cl = v;
                        break;
                    case "transfer-encoding":
                        hasTe = true;
                        break;
                    case "connection":
                        foreach (var t in v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                            named.Add(t);
                        break;
                    case "upgrade":
                        if (v.Contains("websocket", StringComparison.OrdinalIgnoreCase))
                        { error = "WebSocket upgrade cannot be translated to HTTP/2"; return false; }
                        break;
                }
            }
            if (hasTe && (cl != null || !h.TransferEncodingChunked))
            { error = "Unsupported or ambiguous message framing"; return false; }

            result.Add(new(":method", h.HTTPVerb));
            result.Add(new(":scheme", secure ? "https" : "http"));
            result.Add(new(":authority", authority));
            result.Add(new(":path", h.HTTPTargetPath));
            foreach (var (n, v) in fields)
            {
                if (n == "te") { if (v.Equals("trailers", StringComparison.OrdinalIgnoreCase)) result.Add(new(n, v)); continue; }
                if (HopByHop.Contains(n) || named.Contains(n)) continue;
                result.Add(new(n, v));
            }
            return true;
        }

        public static List<H2Header> BuildTrailers(IEnumerable<string> lines)
        {
            var list = new List<H2Header>();
            foreach (var line in lines)
            {
                if (!TrySplit(line, out var n, out var v) || ForbiddenTrailers.Contains(n)) continue;
                list.Add(new(n, v));
            }
            return list;
        }

        // ---------------- h2 response -> h1 bytes ----------------

        public static byte[] BuildResponseHead(IReadOnlyList<H2Header> headers, string? requestMethod,
            bool closeConnection, bool endStream, out int status, out BodyMode mode, out long contentLength)
        {
            string? statusText = null;
            contentLength = -1;
            var lines = new List<string>();
            foreach (var (n, v) in headers)
            {
                if (HasCtl(n) || HasCtl(v)) throw new InvalidDataException("Control characters in header");
                if (n.StartsWith(':'))
                {
                    if (n != ":status") throw new InvalidDataException("Unexpected pseudo-header " + n);
                    statusText = v;
                    continue;
                }
                if (HopByHop.Contains(n)) continue;
                if (n == "content-length")
                {
                    if (!long.TryParse(v, out var cl) || cl < 0 || (contentLength >= 0 && cl != contentLength))
                        throw new InvalidDataException("Invalid content-length");
                    if (contentLength >= 0) continue; // duplicate with the same value
                    contentLength = cl;
                }
                lines.Add($"{n}: {v}");
            }

            if (!int.TryParse(statusText, out status) || status < 100 || status > 599 || status == 101)
                throw new InvalidDataException("Invalid :status");

            bool noBody = requestMethod == "HEAD" || status < 200 || status == 204 || status == 304;
            if (noBody) mode = BodyMode.None;
            else if (contentLength >= 0) mode = BodyMode.ContentLength;
            else if (endStream) { mode = BodyMode.ContentLength; contentLength = 0; lines.Add("content-length: 0"); }
            else { mode = BodyMode.Chunked; lines.Add("transfer-encoding: chunked"); }

            if (closeConnection && status >= 200) lines.Add("connection: close");

            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 ").Append(status).Append(' ').Append(Reason(status)).Append("\r\n");
            foreach (var l in lines) sb.Append(l).Append("\r\n");
            sb.Append("\r\n");
            return Encoding.Latin1.GetBytes(sb.ToString());
        }

        public static byte[] BuildChunkedTrailer(IReadOnlyList<H2Header> headers)
        {
            var sb = new StringBuilder("0\r\n");
            foreach (var (n, v) in headers)
            {
                if (n.StartsWith(':') || ForbiddenTrailers.Contains(n)) continue;
                if (HasCtl(n) || HasCtl(v)) throw new InvalidDataException("Control characters in trailer");
                sb.Append(n).Append(": ").Append(v).Append("\r\n");
            }
            sb.Append("\r\n");
            return Encoding.Latin1.GetBytes(sb.ToString());
        }

        private static string Reason(int status)
        {
            var name = Enum.IsDefined(typeof(HttpStatusCode), status) ? ((HttpStatusCode)status).ToString() : "";
            return ReasonSplit().Replace(name, " ");
        }

        [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
        private static partial Regex ReasonSplit();
    }
}
