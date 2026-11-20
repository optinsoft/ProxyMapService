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

        /// <summary>
        /// Parses the SNI hostname offsets from a complete ClientHello TLS record
        /// (5-byte record header included). Does not require the handshake to fit into a
        /// single record beyond the server_name extension itself.
        /// </summary>
        /// <returns>True and the absolute offsets of the SNI hostname inside the record,
        /// or false when the record is not a ClientHello containing a server_name extension.</returns>
        public static bool TryGetSniOffsets(ReadOnlySpan<byte> d, out int sniStart, out int sniEnd)
        {
            sniStart = sniEnd = -1;

            // TLS record: 0x16 | ver(2) | len(2) | handshake: 0x01 | len(3) | ...
            int len = d.Length;
            if (len < 9 || d[0] != 0x16 || d[5] != 0x01) return false;
            int p = 9 + 2 + 32;                          // client_version + random
            if (p >= len) return false;
            p += 1 + d[p];                               // session_id
            if (p + 2 > len) return false;
            p += 2 + ((d[p] << 8) | d[p + 1]);           // cipher_suites
            if (p >= len) return false;
            p += 1 + d[p];                               // compression
            if (p + 2 > len) return false;
            int extEnd = p + 2 + ((d[p] << 8) | d[p + 1]);
            if (extEnd > len) extEnd = len;
            p += 2;

            while (p + 4 <= extEnd && p + 4 <= len)
            {
                int type = (d[p] << 8) | d[p + 1], extLen = (d[p + 2] << 8) | d[p + 3]; p += 4;
                if (p + extLen > len) return false;
                if (type == 0x0000 && extLen > 5)         // server_name
                {
                    int nameLen = (d[p + 3] << 8) | d[p + 4];
                    sniStart = p + 5;
                    sniEnd = sniStart + nameLen;
                    return sniEnd <= len;
                }
                p += extLen;
            }
            return false;
        }
    }
}
