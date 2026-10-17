using System.Net.Security;

namespace ProxyMapService.Proxy.Tls
{
    public class TlsClientHelloParser
    {
        public static ClientHelloInfo? ParseClientHello(ReadOnlySpan<byte> d)
        {
            // TLS record: 0x16 | ver(2) | len(2) | handshake: 0x01 | len(3) | ...
            if (d.Length < 9 || d[0] != 0x16 || d[5] != 0x01) return null;
            int p = 9 + 2 + 32;                          // client_version + random
            p += 1 + d[p];                               // session_id
            p += 2 + ((d[p] << 8) | d[p + 1]);           // cipher_suites
            p += 1 + d[p];                               // compression
            int extEnd = p + 2 + ((d[p] << 8) | d[p + 1]); p += 2;

            string? sni = null; 
            List<SslApplicationProtocol>? alpn = null;
            while (p + 4 <= extEnd && p + 4 <= d.Length)
            {
                int type = (d[p] << 8) | d[p + 1], len = (d[p + 2] << 8) | d[p + 3]; p += 4;
                if (p + len > d.Length) return null;
                var ext = d.Slice(p, len);
                if (type == 0x0000 && len > 5)           // server_name
                    sni = System.Text.Encoding.ASCII.GetString(ext.Slice(5, (ext[3] << 8) | ext[4]));
                else if (type == 0x0010)                 // ALPN
                {
                    alpn = new();
                    int q = 2, end = 2 + ((ext[0] << 8) | ext[1]);
                    while (q < end) { int l = ext[q++]; alpn.Add(new SslApplicationProtocol(ext.Slice(q, l).ToArray())); q += l; }
                }
                p += len;
            }
            return new(sni, alpn);
        }
    }
}
