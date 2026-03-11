using Microsoft.AspNetCore.Razor.TagHelpers;
using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Network;
using System;
using System.Text;

namespace ProxyMapService.Proxy.Http
{
    public class HttpHeaderRewriter
    {
        private static readonly string[] HttpMethods =
        {
            "GET", "POST", "PUT", "DELETE",
            "HEAD", "OPTIONS", "PATCH", "TRACE", "CONNECT"
        };

        private static readonly string[] HttpMethodPrefixes =
            HttpMethods.Select(m => m + " ").ToArray();

        private static readonly string[] HttpProtos =
        {
            "http", "https"
        };

        private static readonly string[] HttpProtoPrefixes =
            HttpProtos.Select(m => m + "://").ToArray();

        private static int StartsWithHttpMethod(ReadOnlySpan<char> line)
        {
            foreach (var method in HttpMethodPrefixes)
            {
                var methodSpan = method.AsSpan();
                int compareLength = methodSpan.Length;
                if (compareLength <= line.Length && line.Slice(0, compareLength).SequenceEqual(methodSpan))
                {
                    return compareLength;
                }
            }
            return 0;
        }

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
            var methodLength = StartsWithHttpMethod(firstLine.AsSpan());
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

        public static bool OverrideHostHeader(string[] lines, HostAddress host)
        {
            bool overwritten = false;

            if (lines.Length > 0)
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                    {
                        var oldHost = lines[i].AsSpan(5).TrimStart();
                        var port = oldHost.IndexOf(':');
                        if (port != -1)
                        {
                            oldHost = oldHost.Slice(0, port);
                        }
                        if (oldHost.Equals(host.OriginalHostname, StringComparison.OrdinalIgnoreCase))
                        {
                            lines[i] = $"Host: {host.Hostname}:{host.Port}";
                            overwritten = true;
                        }
                        break;
                    }
                }
            }

            return overwritten;
        }
    }
}
