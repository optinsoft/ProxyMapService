// NssJa4Probe — a console utility that prints the JA4 TLS fingerprint of the
// ClientHello produced by NssStream (Firefox-like, NSS-backed).
//
// It starts a local raw TCP capture server, opens an NssStream connection to
// it, captures the ClientHello bytes and computes the JA4 fingerprint
// (FoxIO, https://github.com/FoxIO-llc/ja4) from the raw bytes.
//
// Usage:
//   dotnet run --project NssJa4Probe
//
// The JA4 depends on the version of the linked NSS: features unsupported by
// the linked NSS version (ECH GREASE, ML-KEM X25519MLKEM768, ML-DSA signature
// algorithms, brotli) are silently skipped.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

const string targetHost = "localhost";

// ---------------------------------------------------------------------------
// A minimal raw TCP capture server: accepts one connection, reads the ClientHello
// handshake message and prints the JA4 fingerprint.
// ---------------------------------------------------------------------------

var hellos = new ConcurrentQueue<byte[]>();
var helloReceived = new TaskCompletionSource();

using var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
int port = ((IPEndPoint)listener.LocalEndpoint).Port;
Console.WriteLine($"Capture server listening on 127.0.0.1:{port}");

var acceptLoop = Task.Run(async () =>
{
    using TcpClient client = await listener.AcceptTcpClientAsync();
    using NetworkStream stream = client.GetStream();

    // Read the first 5-byte TLS record header (type + version + length),
    // then the record body (the ClientHello handshake message).
    byte[] header = new byte[5];
    int filled = 0;
    while (filled < 5)
    {
        int n = await stream.ReadAsync(header.AsMemory(filled, 5 - filled));
        if (n == 0) return;
        filled += n;
    }

    if (header[0] != 0x16) return; // not a handshake record
    int recordLen = (header[3] << 8) | header[4];

    // The ClientHello may span multiple TLS records; accumulate until the
    // handshake message is complete.
    var body = new byte[recordLen];
    int bodyFilled = 0;
    while (bodyFilled < recordLen)
    {
        int n = await stream.ReadAsync(body.AsMemory(bodyFilled, recordLen - bodyFilled));
        if (n == 0) return;
        bodyFilled += n;
    }

    // body starts with the 4-byte handshake header (type + 3-byte length);
    // skip it and take the ClientHello body.
    int helloLen = (body[1] << 16) | (body[2] << 8) | body[3];
    if (body[0] != 0x01 || 4 + helloLen > bodyFilled) return; // not a full ClientHello

    hellos.Enqueue(body[4..(4 + helloLen)]);
    helloReceived.TrySetResult();
});

// ---------------------------------------------------------------------------
// Open an NssStream connection to the capture server.
// ---------------------------------------------------------------------------

using var tcp = new TcpClient();
await tcp.ConnectAsync(IPAddress.Loopback, port);
await using NetworkStream inner = tcp.GetStream();

using var nss = new NssSsl.NssStream(inner, targetHost);
var options = new SslClientAuthenticationOptions
{
    TargetHost = targetHost,
    ApplicationProtocols = [new SslApplicationProtocol("h2"), new SslApplicationProtocol("http/1.1")],
};

try
{
    await nss.AuthenticateAsClientAsync(options, CancellationToken.None);
}
catch (Exception ex)
{
    Console.WriteLine($"TLS handshake ended: {ex.GetType().Name}: {ex.Message}");
}

// ---------------------------------------------------------------------------
// Compute and print the JA4 fingerprint.
// ---------------------------------------------------------------------------

await helloReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
if (!hellos.TryDequeue(out var clientHello))
{
    Console.WriteLine("No ClientHello was captured.");
    return 1;
}

string? ja4 = Ja4.Compute(clientHello, out var rawCiphers, out var rawExtensions);
Console.WriteLine();
Console.WriteLine($"JA4:   {ja4}");
Console.WriteLine($"JA4_r: t..._{rawCiphers}_{rawExtensions}");
Console.WriteLine();
Console.WriteLine("Cipher suites:");
foreach (var cipher in rawCiphers.Split(','))
{
    Console.WriteLine($"    {cipher}");
}
Console.WriteLine("Extensions:");
foreach (var ext in rawExtensions.Split(','))
{
    Console.WriteLine($"    {ext}");
}
Console.WriteLine();

return 0;

// ---------------------------------------------------------------------------
// JA4 fingerprint computation (FoxIO ja4.py port).
// ---------------------------------------------------------------------------

static class Ja4
{
    private static bool IsGrease(int value) => (value & 0x0f0f) == 0x0a0a;

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
                        case 0x0000: hasSni = true; break;                // server_name
                        case 0x0010: alpn = ParseAlpn(ext); break;        // ALPN
                        case 0x002b: supportedVersions = ParseU16List(ext, 1); break;    // supported_versions
                        case 0x000d: signatureAlgorithms = ParseU16List(ext, 2); break;  // signature_algorithms
                    }
                    p += len;
                }
            }

            // --- version: supported_versions wins over legacy_version ---
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

    private static string Sha12(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
    }

    private static List<string> ParseAlpn(ReadOnlySpan<byte> ext)
    {
        var result = new List<string>();
        if (ext.Length < 2) return result;
        int totalLen = (ext[0] << 8) | ext[1];
        int p = 2;
        int end = Math.Min(2 + totalLen, ext.Length);
        while (p + 1 < end)
        {
            int len = ext[p++];
            if (p + len > end) break;
            result.Add(Encoding.ASCII.GetString(ext.Slice(p, len)));
            p += len;
        }
        return result;
    }

    private static int[] ParseU16List(ReadOnlySpan<byte> ext, int skip)
    {
        if (ext.Length < 2) return [];
        int totalLen = (ext[0] << 8) | ext[1];
        var result = new List<int>();
        for (int p = skip; p + 1 < Math.Min(2 + totalLen, ext.Length); p += 2)
        {
            result.Add((ext[p] << 8) | ext[p + 1]);
        }
        return result.ToArray();
    }
}
