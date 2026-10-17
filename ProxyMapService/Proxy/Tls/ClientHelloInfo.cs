using System.Net.Security;

namespace ProxyMapService.Proxy.Tls
{
    public sealed record ClientHelloInfo(string? Sni, List<SslApplicationProtocol>? Alpn);
}
