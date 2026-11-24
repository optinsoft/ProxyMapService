// Ignore Spelling: Rewriter

using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Network;
using System.Text;

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
        /// Applies a <see cref="HeadersRule"/> to HTTP/1.1 header lines: the first line (request or status
        /// line) is left unchanged; every other line is either removed (rule.Remove) or replaced by the
        /// Set value (rule.Set, appended in place of the last occurrence). Empty lines (the end of the
        /// header section) are never touched; Set headers are inserted before them.
        /// Returns true when at least one header was modified.
        /// </summary>
        public static bool ApplyHeadersRule(HeadersRule? rule, List<string> headerLines)
        {
            if (rule == null || rule.IsEmpty || headerLines.Count == 0)
            {
                return false;
            }

            bool changed = false;

            if (rule.Remove is { Count: > 0 })
            {
                for (int i = headerLines.Count - 1; i >= 1; i--)
                {
                    var line = headerLines[i];
                    if (line.Length == 0) continue;
                    foreach (var name in rule.Remove)
                    {
                        if (MatchesHeaderName(line, name))
                        {
                            headerLines.RemoveAt(i);
                            changed = true;
                            break;
                        }
                    }
                }
            }

            if (rule.Set is { Count: > 0 })
            {
                int insertAt = headerLines.Count;
                for (int i = 1; i < headerLines.Count; i++)
                {
                    if (headerLines[i].Length == 0)
                    {
                        insertAt = i;
                        break;
                    }
                }
                foreach (var (name, value) in rule.Set)
                {
                    for (int i = headerLines.Count - 1; i >= 1; i--)
                    {
                        if (headerLines[i].Length > 0 && MatchesHeaderName(headerLines[i], name))
                        {
                            headerLines.RemoveAt(i);
                            if (i < insertAt)
                            {
                                insertAt--;
                            }
                            changed = true;
                        }
                    }
                    headerLines.Insert(insertAt, $"{name}: {ResolveValuePlaceholders(value)}");
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// The HTTP/2 counterpart of <see cref="ApplyHeadersRule(HeadersRule?, List&lt;string&gt;)"/> for a decoded
        /// header block: pseudo-headers (":...") are never touched; Set headers are appended after the
        /// existing ones. Returns true when at least one header was modified.
        /// </summary>
        public static bool ApplyHeadersRule(HeadersRule? rule, List<H2Header> headers)
        {
            if (rule == null || rule.IsEmpty)
            {
                return false;
            }

            bool changed = false;

            if (rule.Remove is { Count: > 0 })
            {
                for (int i = headers.Count - 1; i >= 0; i--)
                {
                    var name = headers[i].Name;
                    if (name.StartsWith(':')) continue;
                    foreach (var removeName in rule.Remove)
                    {
                        if (name.Equals(removeName, StringComparison.OrdinalIgnoreCase))
                        {
                            headers.RemoveAt(i);
                            changed = true;
                            break;
                        }
                    }
                }
            }

            if (rule.Set is { Count: > 0 })
            {
                foreach (var (name, value) in rule.Set)
                {
                    if (name.StartsWith(':')) continue; // pseudo-headers cannot be set
                    for (int i = headers.Count - 1; i >= 0; i--)
                    {
                        if (!headers[i].Name.StartsWith(':') && headers[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            headers.RemoveAt(i);
                            changed = true;
                        }
                    }
                    headers.Add(new H2Header(name, ResolveValuePlaceholders(value)));
                    changed = true;
                }
            }

            return changed;
        }

        private static bool MatchesHeaderName(string line, string name)
        {
            return line.Length > name.Length
                && line[name.Length] == ':'
                && line.StartsWith(name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Substitutes "${VARIABLE}" placeholders with environment variables (a missing variable is
        /// substituted with an empty string) and strips CR/LF from the result (header injection guard).
        /// </summary>
        private static string ResolveValuePlaceholders(string value)
        {
            var result = value;
            if (result.Contains("${"))
            {
                var sb = new StringBuilder();
                int pos = 0;
                while (true)
                {
                    int start = result.IndexOf("${", pos, StringComparison.Ordinal);
                    if (start == -1)
                    {
                        sb.Append(result, pos, result.Length - pos);
                        break;
                    }
                    sb.Append(result, pos, start - pos);
                    int end = result.IndexOf('}', start + 2);
                    if (end == -1)
                    {
                        sb.Append(result, start, result.Length - start);
                        break;
                    }
                    var variable = result[(start + 2)..end];
                    var envValue = Environment.GetEnvironmentVariable(variable);
                    if (envValue != null)
                    {
                        sb.Append(envValue);
                    }
                    pos = end + 1;
                }
                result = sb.ToString();
            }
            return result.Replace("\r", "").Replace("\n", "").Trim();
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
