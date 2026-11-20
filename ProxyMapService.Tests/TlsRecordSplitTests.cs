using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Tls;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// Tests for the Listen/HostRule TlsRecordSplit setting: splitting the first
    /// outgoing TLS record (ClientHello) into several separate TLS records.
    /// </summary>
    public class TlsRecordSplitTests
    {
        private const int PayloadLen = 100;

        private static byte[] MakeRecord(int payloadLen = PayloadLen)
        {
            var record = new byte[5 + payloadLen];
            record[0] = 0x16;                       // handshake
            record[1] = 0x03; record[2] = 0x01;     // TLS 1.0 record version
            record[3] = (byte)(payloadLen >> 8);
            record[4] = (byte)payloadLen;
            for (int i = 0; i < payloadLen; i++) record[5 + i] = (byte)(i + 1);
            return record;
        }

        /// <summary>Parses the concatenated split output and returns the record payloads.</summary>
        private static List<byte[]> ParseRecords(byte[] output)
        {
            var payloads = new List<byte[]>();
            int pos = 0;
            while (pos < output.Length)
            {
                Assert.True(pos + 5 <= output.Length, "truncated record header");
                Assert.Equal(0x16, output[pos]);                    // content type preserved
                int len = (output[pos + 3] << 8) | output[pos + 4];
                Assert.True(len > 0 && pos + 5 + len <= output.Length, "bad record length");
                payloads.Add(output[(pos + 5)..(pos + 5 + len)]);
                pos += 5 + len;
            }
            return payloads;
        }

        [Fact]
        public void Parse_Empty_ReturnsNull()
        {
            Assert.Null(TlsRecordSplitSpec.Parse(null));
            Assert.Null(TlsRecordSplitSpec.Parse(""));
            Assert.Null(TlsRecordSplitSpec.Parse("  "));
        }

        [Theory]
        [InlineData("x")]
        [InlineData("1+x")]
        [InlineData("1:0:0")]
        [InlineData("-:2")]
        public void Parse_Invalid_Throws(string value)
        {
            Assert.Throws<FormatException>(() => TlsRecordSplitSpec.Parse(value));
        }

        [Fact]
        public void AbsoluteSplit_SplitsIntoTwoRecords()
        {
            var spec = TlsRecordSplitSpec.Parse("1")!;
            var split = spec.BuildSplitRecords(MakeRecord(), null, null)!;

            var payloads = ParseRecords(split);
            Assert.True(payloads.Count == 2, "expected 2 records");
            // split after the first ClientHello byte (the handshake type), spec "1"
            Assert.True(payloads[0].Length == 1, "first record payload must be 1 byte");
            // the chunks reassemble into the original record payload
            Assert.Equal(MakeRecord()[5..], payloads[0].Concat(payloads[1]).ToArray());
        }

        [Fact]
        public void SniRelativeSplit_AppliesSniStartOffset()
        {
            var spec = TlsRecordSplitSpec.Parse("3+s")!;
            const int sniStart = 20;
            var split = spec.BuildSplitRecords(MakeRecord(), sniStart, sniStart + 14)!;

            var payloads = ParseRecords(split);
            Assert.True(payloads.Count == 2, "expected 2 records");
            Assert.Equal(sniStart - 5 + 3, payloads[0].Length);   // split after the 3rd SNI byte
            Assert.Equal(MakeRecord()[5..], payloads[0].Concat(payloads[1]).ToArray());
        }

        [Theory]
        [InlineData("0+se", 20, 30, 25)]    // end of SNI (25 = 30 - 5 header bytes)
        [InlineData("0+sm", 20, 30, 20)]    // middle of SNI
        [InlineData("0+s", 20, 30, 15)]     // start of SNI
        [InlineData("0+m", 20, 30, 50)]     // middle of the ClientHello
        [InlineData("-5", 20, 30, 95)]      // 5 bytes from the end
        public void FlagResolution_ResolvesExpectedPositions(string specValue, int sniStart, int sniEnd, int expected)
        {
            var spec = TlsRecordSplitSpec.Parse(specValue)!;
            var split = spec.BuildSplitRecords(MakeRecord(), sniStart, sniEnd)!;

            var payloads = ParseRecords(split);
            Assert.Equal(expected, payloads[0].Length);
            Assert.Equal(MakeRecord()[5..], payloads.SelectMany(p => p).ToArray());
        }

        [Fact]
        public void SplitAtRecordEnd_IsANoop()
        {
            // "0+e" resolves to the end of the record — nothing to split
            var spec = TlsRecordSplitSpec.Parse("0+e")!;
            Assert.Null(spec.BuildSplitRecords(MakeRecord(), 20, 30));
        }

        [Fact]
        public void NegativeOffset_CountsFromRecordEnd()
        {
            var spec = TlsRecordSplitSpec.Parse("-5")!;
            var split = spec.BuildSplitRecords(MakeRecord(), null, null)!;

            var payloads = ParseRecords(split);
            Assert.Equal(PayloadLen - 5, payloads[0].Length);
            Assert.Equal(MakeRecord()[5..], payloads[0].Concat(payloads[1]).ToArray());
        }

        [Fact]
        public void RepeatsExpandToMultiplePoints()
        {
            var spec = TlsRecordSplitSpec.Parse("1:3:5")!;   // positions 1, 6, 11
            var split = spec.BuildSplitRecords(MakeRecord(), null, null)!;

            var payloads = ParseRecords(split);
            Assert.True(payloads.Count == 4, "expected 4 records");
            Assert.True(payloads[0].Length == 1, "first record payload must be 1 byte");
            Assert.Equal(5, payloads[1].Length);
            Assert.Equal(5, payloads[2].Length);
            Assert.Equal(MakeRecord()[5..], payloads.SelectMany(p => p).ToArray());
        }

        [Fact]
        public void SniDependentPoint_WithoutSni_IsSkipped()
        {
            var spec = TlsRecordSplitSpec.Parse("3+s")!;
            Assert.Null(spec.BuildSplitRecords(MakeRecord(), null, null));
        }

        [Fact]
        public void OutOfRangePoints_AreIgnored()
        {
            // 1000 exceeds the record length, -1000 is before its start
            var spec = TlsRecordSplitSpec.Parse("1000,-1000")!;
            Assert.Null(spec.BuildSplitRecords(MakeRecord(), null, null));
        }

        // ---------- TlsRecordSplitStream ----------

        [Fact]
        public async Task Stream_BuffersPartialWrites_AndSplitsFirstRecord()
        {
            var spec = TlsRecordSplitSpec.Parse("1")!;
            await using var target = new MemoryStream();
            using var stream = new TlsRecordSplitStream(target, spec);

            var record = MakeRecord();
            await stream.WriteAsync(record.AsMemory(0, 3));            // incomplete header
            await stream.WriteAsync(record.AsMemory(3, 10));           // incomplete record
            await stream.WriteAsync(record.AsMemory(13, 10));          // incomplete record
            await stream.WriteAsync(record.AsMemory(23));              // completes the record

            var output = target.ToArray();
            var payloads = ParseRecords(output);
            Assert.True(payloads.Count == 2, "expected 2 records");
            Assert.Equal(record[5..], payloads[0].Concat(payloads[1]).ToArray());
        }

        [Fact]
        public async Task Stream_SecondRecord_PassesThroughUnchanged()
        {
            var spec = TlsRecordSplitSpec.Parse("1")!;
            await using var target = new MemoryStream();
            using var stream = new TlsRecordSplitStream(target, spec);

            var record = MakeRecord();
            await stream.WriteAsync(record);
            var second = MakeRecord(50);
            await stream.WriteAsync(second);

            var output = target.ToArray();
            var payloads = ParseRecords(output);
            Assert.True(payloads.Count == 3, "expected 3 records");                           // split first + whole second
            Assert.Equal(second[5..], payloads[2]);
        }

        [Fact]
        public async Task Stream_NonHandshakeRecord_IsNotSplit()
        {
            var spec = TlsRecordSplitSpec.Parse("1")!;
            await using var target = new MemoryStream();
            using var stream = new TlsRecordSplitStream(target, spec);

            var record = MakeRecord();
            record[0] = 0x17;                                          // application data
            await stream.WriteAsync(record);

            Assert.Equal(record, target.ToArray());
        }

        // ---------- SNI offsets over a real ClientHello ----------

        [Fact]
        public async Task TryGetSniOffsets_ResolvesRealClientHelloSni()
        {
            await using var capture = new ClientHelloCaptureServer();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, capture.Port);
            using (var ssl = new SslStream(client.GetStream()))
            {
                var handshake = ssl.AuthenticateAsClientAsync("example.com");
                var hello = await capture.FirstHelloAsync(TimeSpan.FromSeconds(15));

                // FirstHelloAsync returns the ClientHello handshake message without the
                // record and handshake headers — prepend both so the offsets are
                // record-relative.
                var record = new byte[5 + 4 + hello.Length];
                record[0] = 0x16;
                record[1] = 0x03; record[2] = 0x01;
                int handshakeLen = 4 + hello.Length;
                record[3] = (byte)(handshakeLen >> 8);
                record[4] = (byte)handshakeLen;
                record[5] = 0x01;
                record[6] = (byte)(hello.Length >> 16);
                record[7] = (byte)(hello.Length >> 8);
                record[8] = (byte)hello.Length;
                hello.CopyTo(record, 9);

                Assert.True(TlsClientHelloParser.TryGetSniOffsets(record, out var sniStart, out var sniEnd));
                Assert.Equal("example.com", Encoding.ASCII.GetString(record[sniStart..sniEnd]));
                try
                {
                    await handshake; // the capture server closes the connection before completing the handshake
                }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
                {
                    // Expected: the connection is dropped by the capture server.
                }
            }
        }

        // ---------- end-to-end handshake with split ClientHello ----------

        [Fact]
        public async Task SslStream_Handshake_Completes_With_Split_ClientHello()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            string? echo = null;
            try
            {
                var clientTask = Task.Run(async () =>
                {
                    using var tcp = new TcpClient();
                    await tcp.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                    using var raw = tcp.GetStream();
                    using var splitter = new TlsRecordSplitStream(raw, TlsRecordSplitSpec.Parse("6")!);
                    // splitter implements ITunnelStream — required by the tunnels
                    using var ssl = new SslStream(splitter);
                    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = "localhost",
                        EnabledSslProtocols = SslProtocols.Tls12,
                        RemoteCertificateValidationCallback = (_, _, _, _) => true,
                    });
                    await ssl.WriteAsync(Encoding.UTF8.GetBytes("ping"));
                    var buf = new byte[16];
                    int n = await ssl.ReadAsync(buf);
                    return Encoding.UTF8.GetString(buf, 0, n);
                });

                using var server = await listener.AcceptTcpClientAsync();
                using var serverStream = server.GetStream();

                // Complete the handshake as a normal server, but over a tapping stream
                // that captures the raw bytes of the first read (the split ClientHello).
                using var tap = new TapStream(serverStream);
                using var serverSsl = new SslStream(tap);
                using var certificate = CreateSelfSignedCertificate("localhost");
                await serverSsl.AuthenticateAsServerAsync(certificate);
                var request = new byte[4];
                Assert.Equal(4, await serverSsl.ReadAsync(request));
                await serverSsl.WriteAsync(request);

                echo = await clientTask.WaitAsync(TimeSpan.FromSeconds(10));

                // The captured first read must contain more than one TLS handshake record:
                // the first record is only 6 bytes long (spec "6"), the next header follows.
                var captured = tap.FirstRead.ToArray();
                Assert.True(captured.Length > 11, "no second record in the first read");
                Assert.Equal(0x16, captured[0]);
                Assert.Equal(6, (captured[3] << 8) | captured[4]);   // first split record: 6 bytes
                Assert.Equal(0x16, captured[11]);                    // second record header present
            }
            finally
            {
                listener.Stop();
            }

            Assert.Equal("ping", echo);
        }

        [Fact]
        public async Task PassthroughSplit_DelegatesCountingToInnerCountingStream()
        {
            // In passthrough mode (no MITM) the splitter replaces context.OutgoingStream as
            // the tunnel destination; it is a plain ITunnelStream wrapper that only forwards
            // the tunnel members to the inner CountingStream, which keeps counting raw bytes.
            var sendCounter = new FakeSendCounter();
            var memory = new MemoryStream();
            var inner = new CountingStream(memory, null!, null, sendCounter, 0, 0);
            using var splitter = new TlsRecordSplitStream(inner, TlsRecordSplitSpec.Parse("6")!);

            var record = MakeRecord();
            await splitter.WriteAsync(record);

            var payloads = ParseRecords(memory.ToArray());
            Assert.Equal(2, payloads.Count);
            Assert.Equal(record[5..], payloads.SelectMany(p => p).ToArray());

            // Counted exactly once — by the inner CountingStream, with the split sizes.
            Assert.Equal(memory.Length, sendCounter.TotalBytesSent);
            Assert.Equal(2, sendCounter.Writes.Count);
            Assert.Equal(11, sendCounter.Writes[0]);           // 5-byte header + 6-byte payload
            Assert.Equal(5 + PayloadLen - 6, sendCounter.Writes[1]);
        }

        /// <summary>Fake send counter recording the sizes of counted writes.</summary>
        private sealed class FakeSendCounter : IBytesSendCounter
        {
            public readonly List<int> Writes = [];
            public StreamDirection Direction => StreamDirection.Upstream;
            public long TotalBytesSent => Writes.Sum();
            public long ProxyBytesSent => TotalBytesSent;
            public long BypassBytesSent => 0;
            public long CacheBytesSent => 0;
            public bool IsLogSending => false;
            public void Reset() => Writes.Clear();
            public void OnBytesSend(SessionContext context, int bytesSend, byte[]? bytesData, int startIndex, long tunnelId) => Writes.Add(bytesSend);
            public void OnBytesSent(SessionContext context, int bytesSent, byte[]? bytesData, int startIndex, long tunnelId) { }
        }

        /// <summary>Stream wrapper capturing the bytes of the first read(s).</summary>
        private sealed class TapStream : Stream
        {
            private readonly Stream _inner;

            public MemoryStream FirstRead { get; } = new();

            public TapStream(Stream inner) => _inner = inner;

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                int read = await _inner.ReadAsync(buffer, cancellationToken);
                if (read > 0 && FirstRead.Length < 16 * 1024)
                {
                    await FirstRead.WriteAsync(buffer[..read], cancellationToken);
                }
                return read;
            }

            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);
            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
        }

        private static X509Certificate2 CreateSelfSignedCertificate(string hostname)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                $"CN={hostname}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        }
    }
}
