using System.Security.Cryptography;
using System.Text;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// Computes the JA4 TLS fingerprint (FoxIO, https://github.com/FoxIO-LLC/ja4)
    /// from raw TLS ClientHello bytes — a C# port of the reference ja4.py logic.
    /// Format: t{version}{sni}{cipher_count}{ext_count}{alpn}_{cipher_hash}_{ext_hash}
    /// Example (Chrome): t13d1516h2_8daaf6152771_e5627efa2ab1
    /// </summary>
    public static class Ja4Fingerprint
    {
        private static bool IsGrease(int value) => (value & 0x0f0f) == 0x0a0a;

        /// <param name="clientHello">
        /// The ClientHello handshake message body (everything after the 4-byte
        /// handshake header, i.e. starting at client_version).
        /// </param>
        /// <param name="rawCiphers">Sorted cipher suites used for the hash, for diagnostics.</param>
        /// <param name="rawExtensions">Sorted extensions (+ signature algorithms) used for the hash, for diagnostics.</param>
        public static string? Compute(byte[] clientHello, out string rawCiphers, out string rawExtensions)
        {
            rawCiphers = string.Empty;
            rawExtensions = string.Empty;

            var d = clientHello;
            try
            {
                int p = 0;
                int legacyVersion = (d[p] << 8) | d[p + 1]; p += 2;
                p += 32;                                                    // random
                p += 1 + d[p];                                              // session_id
                int cipherSuitesLen = (d[p] << 8) | d[p + 1]; p += 2;
                var ciphers = new List<int>(cipherSuitesLen / 2);
                for (int i = 0; i < cipherSuitesLen / 2; i++)
                {
                    ciphers.Add((d[p] << 8) | d[p + 1]); p += 2;
                }
                p += 1 + d[p];                                              // compression_methods

                bool hasSni = false;
                List<string>? alpn = null;
                int[] supportedVersions = [];
                int[] signatureAlgorithms = [];
                var extensionTypes = new List<int>();

                if (p + 2 <= d.Length)
                {
                    int extensionsLen = (d[p] << 8) | d[p + 1]; p += 2;
                    int end = Math.Min(p + extensionsLen, d.Length);
                    while (p + 4 <= end)
                    {
                        int type = (d[p] << 8) | d[p + 1];
                        int len = (d[p + 2] << 8) | d[p + 3];
                        p += 4;
                        if (p + len > d.Length) return null;
                        var ext = d.AsSpan(p, len);
                        extensionTypes.Add(type);
                        switch (type)
                        {
                            case 0x0000: hasSni = true; break;                  // server_name
                            case 0x0010: alpn = ParseAlpn(ext); break;          // ALPN
                            case 0x002b: supportedVersions = ParseU16List(ext, 1); break;      // supported_versions
                            case 0x000d: signatureAlgorithms = ParseU16List(ext, 2); break;   // signature_algorithms
                        }
                        p += len;
                    }
                }

                // --- version (supported_versions wins over legacy_version) ---
                int version = legacyVersion;
                if (supportedVersions.Length > 0)
                {
                    version = supportedVersions.Where(v => !IsGrease(v)).OrderBy(v => v).Last();
                }

                // --- cipher hash: GREASE removed, sorted, comma-joined hex, SHA256[:12] ---
                var cipherList = ciphers
                    .Where(c => !IsGrease(c))
                    .Select(c => $"{c:x4}")
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList();
                rawCiphers = string.Join(",", cipherList);
                string cipherHash = cipherList.Count > 0 ? Sha12(rawCiphers) : "000000000000";
                string cipherLen = $"{Math.Min(cipherList.Count, 99):D2}";

                // --- extension hash: GREASE removed, SNI(0000) and ALPN(0010) removed,
                //     sorted, then signature algorithms appended after '_') ---
                var exts = extensionTypes.Where(t => !IsGrease(t)).ToList();
                string extLen = $"{Math.Min(exts.Count, 99):D2}";
                var extList = exts
                    .Where(t => t != 0x0000 && t != 0x0010)
                    .Select(t => $"{t:x4}")
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList();
                var sigList = signatureAlgorithms
                    .Where(s => !IsGrease(s))
                    .Select(s => $"{s:x4}")
                    .ToList();
                rawExtensions = sigList.Count > 0
                    ? string.Join(",", extList) + "_" + string.Join(",", sigList)
                    : string.Join(",", extList);
                string extHash = rawExtensions.Length > 0 ? Sha12(rawExtensions) : "000000000000";

                // --- ALPN: first letter + last letter of the first protocol ("h2", "h1", "00") ---
                string alpnPart = "00";
                if (alpn is { Count: > 0 } list && list[0].Length > 0)
                {
                    alpnPart = $"{list[0][0]}{list[0][^1]}";
                }

                string sni = hasSni ? "d" : "i";
                return $"t{MapVersion(version)}{sni}{cipherLen}{extLen}{alpnPart}_{cipherHash}_{extHash}";
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        private static string MapVersion(int version) => version switch
        {
            0x0304 => "13",
            0x0303 => "12",
            0x0302 => "11",
            0x0301 => "10",
            0x0300 => "s3",
            0x0002 => "s2",
            _ => "00"
        };

        private static List<string>? ParseAlpn(ReadOnlySpan<byte> ext)
        {
            if (ext.Length < 2) return null;
            int end = 2 + ((ext[0] << 8) | ext[1]);
            var result = new List<string>();
            int q = 2;
            while (q < end && q < ext.Length)
            {
                int l = ext[q++];
                if (q + l > ext.Length) break;
                result.Add(Encoding.ASCII.GetString(ext.Slice(q, l)));
                q += l;
            }
            return result;
        }

        /// <param name="prefixLen">Length of the list's length field: 1 byte for supported_versions, 2 bytes for signature_algorithms.</param>
        private static int[] ParseU16List(ReadOnlySpan<byte> ext, int prefixLen)
        {
            int start = prefixLen;
            var result = new List<int>((ext.Length - start) / 2);
            for (int i = start; i + 1 < ext.Length; i += 2)
            {
                result.Add((ext[i] << 8) | ext[i + 1]);
            }
            return [.. result];
        }

        private static string Sha12(string value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
        }
    }
}
