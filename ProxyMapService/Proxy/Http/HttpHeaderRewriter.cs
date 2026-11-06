// Ignore Spelling: Rewriter

using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Network;

namespace ProxyMapService.Proxy.Http
{
    public class HttpHeaderRewriter
    {
        private static readonly string[] HttpProtos =
        {
            "http", "https"
        };

        private static readonly string[] HttpProtoPrefixes =
            HttpProtos.Select(m => m + "://").ToArray();

        private static int StartsWithHttpProto(ReadOnlySpan<char> target)
        {
            foreach (var proto in HttpProtoPrefixes)
            {
                var protoSpan = proto.AsSpan();
                int compareLength = protoSpan.Length;
                if (compareLength <= target.Length && target.Slice(0, compareLength).Equals(protoSpan, StringComparison.OrdinalIgnoreCase))
                {
                    return compareLength;
                }
            }
            return 0;
        }

        public static string? OverrideHttpCommandHost(string firstLine, HostAddress host)
        {
            var methodLength = HttpParser.StartsWithHttpMethod(firstLine.AsSpan(), false);
            if (methodLength > 0)
            {
                var method = firstLine.AsSpan(0, methodLength).TrimEnd();
                var target = firstLine.AsSpan(methodLength).TrimStart();
                var targetEnd = target.IndexOf(' ');
                if (targetEnd != -1)
                {
                    var httpProto = target.Slice(targetEnd);
                    target = target.Slice(0, targetEnd);
                    var protoLength = StartsWithHttpProto(target);
                    if (protoLength > 0)
                    {
                        var proto = target.Slice(0, protoLength);
                        var targetHost = target.Slice(protoLength);
                        var targetHostEnd = targetHost.IndexOf('/');
                        if (targetHostEnd != -1)
                        {
                            var targetPath = targetHost.Slice(targetHostEnd);
                            targetHost = targetHost.Slice(0, targetHostEnd);
                            if (targetHost.Equals(host.OriginalHostname, StringComparison.OrdinalIgnoreCase))
                            {
                                return $"{method} {proto}{host.Hostname}:{host.Port}{targetPath} {httpProto}";
                            }
                        }
                    }
                }
            }
            return null;
        }

        public static bool OverrideHostHeader(Span<string> headers, HostAddress host)
        {
            bool overwritten = false;

            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    var oldHost = headers[i].AsSpan(5).TrimStart();
                    var port = oldHost.IndexOf(':');
                    if (port != -1)
                    {
                        oldHost = oldHost.Slice(0, port);
                    }
                    if (oldHost.Equals(host.OriginalHostname, StringComparison.OrdinalIgnoreCase))
                    {
                        headers[i] = $"Host: {host.Hostname}:{host.Port}";
                        overwritten = true;
                    }
                    break;
                }
            }

            return overwritten;
        }

        /// <summary>
        /// The HTTP/2 counterpart of <see cref="OverrideHostHeader(string[], HostAddress)"/> for a decoded header block:
        /// replaces the host with the overridden one in the ":authority" pseudo-header and, if present,
        /// in the regular "host" header. The caller is responsible for checking <see cref="HostAddress.Overwritten"/>.
        /// </summary>
        public static bool OverrideHostHeader(List<H2Header> headers, HostAddress host)
        {
            bool overwritten = false;

            for (int i = 0; i < headers.Count; i++)
            {
                var h = headers[i];
                if (h.Name is not (":authority" or "host")) continue;

                var oldHost = h.Value;
                var port = oldHost.IndexOf(':');
                if (port != -1) oldHost = oldHost[..port];
                if (oldHost.Equals(host.OriginalHostname, StringComparison.OrdinalIgnoreCase))
                {
                    headers[i] = h with { Value = $"{host.Hostname}:{host.Port}" };
                    overwritten = true;
                }
            }

            return overwritten;
        }
    }
}
