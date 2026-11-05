using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Network;
using Xunit;

namespace ProxyMapService.Tests
{
    public class Http2HeaderRewriterTests
    {
        private static readonly byte[] Preface = H2.Preface;

        private static readonly (string, string)[] RequestHeaders =
        [
            (":method", "GET"),
            (":scheme", "https"),
            (":authority", "original.example"),
            (":path", "/a/b"),
            ("user-agent", "TestAgent"),
        ];

        private static HostAddress OverwrittenHost()
        {
            var host = new HostAddress("original.example", 443);
            host.OverrideHostName("127.0.0.1");
            host.OverridePort(8443);
            return host;
        }

        private static Http2HeaderRewriter CreateRewriter(HostAddress? host = null, Func<List<H2Header>, bool>? policy = null)
            => new(
                new HpackDecoderAdapter(Http2TrafficObserver.PassiveMaxDynamicTableSize),
                policy ?? (headers => host == null ? false : HttpHeaderRewriter.OverrideHostHeader(headers, host)));

        /// <summary>Feeds the input to the rewriter in small chunks (frame boundaries are crossed on purpose).</summary>
        private static byte[] TransformAll(Http2HeaderRewriter rewriter, byte[] input)
        {
            var output = new ArrayBufferWriter<byte>(input.Length + 128);
            for (int offset = 0; offset < input.Length; offset += 10)
            {
                int n = Math.Min(10, input.Length - offset);
                rewriter.Transform(input.AsSpan(offset, n), output);
            }
            return output.WrittenSpan.ToArray();
        }

        /// <summary>Parses the rewriter output, skipping the connection preface.</summary>
        private static List<H2Frame> ParseFrames(byte[] data)
        {
            var reader = new H2FrameReader((1 << 24) - 1);
            reader.Append(data.AsSpan(H2.Preface.Length));
            var frames = new List<H2Frame>();
            while (reader.TryRead(out var f)) frames.Add(f);
            Assert.Equal(0, data.Length - H2.Preface.Length - frames.Sum(f => 9 + f.Payload.Length));
            return frames;
        }

        private static byte[] Frame(byte type, byte flags, int stream, byte[] payload)
        {
            var f = new byte[9 + payload.Length];
            f[0] = (byte)(payload.Length >> 16);
            f[1] = (byte)(payload.Length >> 8);
            f[2] = (byte)payload.Length;
            f[3] = type;
            f[4] = flags;
            BinaryPrimitives.WriteInt32BigEndian(f.AsSpan(5, 4), stream);
            payload.CopyTo(f.AsSpan(9));
            return f;
        }

        private static byte[] EncodeBlock(params (string, string)[] headers)
            => new HpackEncoderAdapter().Encode(headers.Select(h => new H2Header(h.Item1, h.Item2)).ToList());

        private static List<H2Header> DecodeBlock(byte[] block)
            => new HpackDecoderAdapter().Decode(block);

        [Fact]
        public void Rewrites_Authority_And_Passes_Other_Headers()
        {
            using var rewriter = CreateRewriter(OverwrittenHost());
            var input = Preface.Concat(Frame(H2.Headers, H2.EndStream | H2.EndHeaders, 1, EncodeBlock(RequestHeaders))).ToArray();

            var output = TransformAll(rewriter, input);
            var frames = ParseFrames(output);

            Assert.Single(frames);
            Assert.Equal(H2.Headers, frames[0].Type);
            Assert.Equal(H2.EndStream | H2.EndHeaders, frames[0].Flags);
            Assert.Equal(1, frames[0].StreamId);

            var decoded = DecodeBlock(frames[0].Payload);
            Assert.Equal("127.0.0.1:8443", Http2StreamInfo.Find(decoded, ":authority"));
            Assert.Equal("GET", Http2StreamInfo.Find(decoded, ":method"));
            Assert.Equal("https", Http2StreamInfo.Find(decoded, ":scheme"));
            Assert.Equal("/a/b", Http2StreamInfo.Find(decoded, ":path"));
            Assert.Equal("TestAgent", Http2StreamInfo.Find(decoded, "user-agent"));
            Assert.Equal(5, decoded.Count);
        }

        [Fact]
        public void Rewrites_Every_Header_Block_Independent_Requests()
        {
            using var rewriter = CreateRewriter(OverwrittenHost());
            var input = Preface
                .Concat(Frame(H2.Headers, H2.EndStream | H2.EndHeaders, 1, EncodeBlock(RequestHeaders)))
                .Concat(Frame(H2.Headers, H2.EndStream | H2.EndHeaders, 3, EncodeBlock(RequestHeaders))).ToArray();

            var frames = ParseFrames(TransformAll(rewriter, input));

            Assert.Equal(2, frames.Count);
            Assert.Equal(new[] { 1, 3 }, frames.Select(f => f.StreamId).ToArray());
            foreach (var f in frames)
                Assert.Equal("127.0.0.1:8443", Http2StreamInfo.Find(DecodeBlock(f.Payload), ":authority"));
        }

        [Fact]
        public void Decodes_Client_Dynamic_Table_References()
        {
            // block 1 adds "x-test: abc" to the client's dynamic table (0x40 = incremental, index 0 = new name,
            // followed by a 7-bit name length), block 2 references it by the dynamic index 62 —
            // the rewriter's decoder must keep the client's table state
            byte[] block1 =
            {
                0x40, 0x06, (byte)'x', (byte)'-', (byte)'t', (byte)'e', (byte)'s', (byte)'t',
                0x03, (byte)'a', (byte)'b', (byte)'c'
            };
            byte[] block2 = [0xBE]; // indexed header field: dynamic index 62 (0x80 | 62)

            using var rewriter = CreateRewriter();
            var input = Preface
                .Concat(Frame(H2.Headers, H2.EndStream | H2.EndHeaders, 1, block1))
                .Concat(Frame(H2.Headers, H2.EndStream | H2.EndHeaders, 3, block2)).ToArray();

            var frames = ParseFrames(TransformAll(rewriter, input));

            Assert.Equal(2, frames.Count);
            Assert.Equal("abc", Http2StreamInfo.Find(DecodeBlock(frames[0].Payload), "x-test"));
            var second = DecodeBlock(frames[1].Payload);
            Assert.Contains(second, h => h.Name == "x-test" && h.Value == "abc");
        }

        [Fact]
        public void Reassembles_Continuation_Blocks()
        {
            using var rewriter = CreateRewriter(OverwrittenHost());
            var block = EncodeBlock(RequestHeaders);
            var part1 = block[..4];
            var part2 = block[4..];

            var input = Preface
                .Concat(Frame(H2.Headers, 0, 1, part1))
                .Concat(Frame(H2.Continuation, H2.EndHeaders, 1, part2)).ToArray();

            var frames = ParseFrames(TransformAll(rewriter, input));

            Assert.Single(frames);
            Assert.Equal(H2.Headers, frames[0].Type);
            Assert.Equal(H2.EndHeaders, frames[0].Flags);
            Assert.Equal("127.0.0.1:8443", Http2StreamInfo.Find(DecodeBlock(frames[0].Payload), ":authority"));
        }

        [Fact]
        public void Passes_Through_Non_Header_Frames()
        {
            using var rewriter = CreateRewriter(OverwrittenHost());
            var dataFrame = Frame(H2.Data, 0, 1, "hello"u8.ToArray());
            var windowUpdate = Frame(H2.WindowUpdate, 0, 0, [0, 0, 0, 64]);

            var input = Preface
                .Concat(dataFrame)
                .Concat(Frame(H2.Headers, H2.EndStream | H2.EndHeaders, 1, EncodeBlock(RequestHeaders)))
                .Concat(windowUpdate).ToArray();

            var frames = ParseFrames(TransformAll(rewriter, input));

            Assert.Equal(3, frames.Count);
            Assert.Equal(H2.Data, frames[0].Type);
            Assert.Equal("hello", Encoding.ASCII.GetString(frames[0].Payload));
            Assert.Equal(H2.Headers, frames[1].Type);
            Assert.Equal(H2.WindowUpdate, frames[2].Type);
            Assert.Equal(64, BinaryPrimitives.ReadInt32BigEndian(frames[2].Payload.AsSpan()));
        }

        [Fact]
        public void Splits_Large_Blocks_Into_Continuation_Frames()
        {
            using var rewriter = CreateRewriter(OverwrittenHost());
            var big = new string('A', 17_000);
            var input = Preface
                .Concat(Frame(H2.Headers, H2.EndStream | H2.EndHeaders, 1, EncodeBlock(
                    (":method", "GET"), (":scheme", "https"),
                    (":authority", "original.example"), (":path", "/"), ("x-big", big)))).ToArray();

            var frames = ParseFrames(TransformAll(rewriter, input));

            Assert.Equal(2, frames.Count);
            Assert.Equal(H2.Headers, frames[0].Type);
            Assert.Equal(H2.EndStream, frames[0].Flags);
            Assert.Equal(16384, frames[0].Payload.Length);
            Assert.Equal(H2.Continuation, frames[1].Type);
            Assert.Equal(H2.EndHeaders, frames[1].Flags);

            var decoded = DecodeBlock(frames[0].Payload.Concat(frames[1].Payload).ToArray());
            Assert.Equal(big, Http2StreamInfo.Find(decoded, "x-big"));
            Assert.Equal("127.0.0.1:8443", Http2StreamInfo.Find(decoded, ":authority"));
        }

        [Fact]
        public void Does_Not_Touch_Other_Hosts()
        {
            using var rewriter = CreateRewriter(OverwrittenHost());
            var input = Preface
                .Concat(Frame(H2.Headers, H2.EndStream | H2.EndHeaders, 1, EncodeBlock(
                    (":method", "GET"), (":scheme", "https"),
                    (":authority", "other.example"), (":path", "/")))).ToArray();

            var frames = ParseFrames(TransformAll(rewriter, input));

            Assert.Single(frames);
            var decoded = DecodeBlock(frames[0].Payload);
            Assert.Equal("other.example", Http2StreamInfo.Find(decoded, ":authority"));
            Assert.Null(Http2StreamInfo.Find(decoded, "x-not-added"));
        }

        [Fact]
        public void Rejects_Push_Promise_From_Client()
        {
            using var rewriter = CreateRewriter();
            var input = Preface.Concat(Frame(H2.PushPromise, 0, 1, [1, 1, 1, 2])).ToArray();

            Assert.Throws<InvalidDataException>(() => TransformAll(rewriter, input));
        }

        [Fact]
        public void Rejects_Continuation_Without_Headers()
        {
            using var rewriter = CreateRewriter();
            var input = Preface.Concat(Frame(H2.Continuation, H2.EndHeaders, 1, [])).ToArray();

            Assert.Throws<InvalidDataException>(() => TransformAll(rewriter, input));
        }

        [Fact]
        public void Reencoded_Blocks_Contain_Only_NonIndexing_Literals()
        {
            // no indexed/incremental representations (0x40+) and no dynamic table size updates (0x20-0x3F):
            // every field is either a known name (0000 + Index(4+), index > 0) followed by a value string,
            // or a new name (0000 0000 + Name(7+)) followed by name and value strings
            using var rewriter = CreateRewriter(OverwrittenHost());
            var input = Preface.Concat(Frame(H2.Headers, H2.EndStream | H2.EndHeaders, 1, EncodeBlock(RequestHeaders))).ToArray();

            var frames = ParseFrames(TransformAll(rewriter, input));
            var payload = frames[0].Payload.AsSpan();
            for (int i = 0; i < payload.Length; )
            {
                byte b = payload[i++];
                Assert.True(b < 0x40, $"byte 0x{b:X2}: indexed or incremental indexing is not allowed");
                Assert.True(b < 0x20, $"byte 0x{b:X2}: dynamic table size update is not allowed");
                if (b == 0)
                {
                    int nameLen = ReadIntegerValue(payload, ref i);
                    int valueLen = ReadIntegerValue(payload, ref i);
                    i += nameLen + valueLen;
                }
                else
                {
                    if (b == 0x0F) ReadIntegerValue(payload, ref i); // extended index
                    int valueLen = ReadIntegerValue(payload, ref i);
                    i += valueLen;
                }
            }
        }

        private static int ReadIntegerValue(ReadOnlySpan<byte> span, ref int offset)
        {
            int value = span[offset] & 0x7F;
            offset++;
            if (value == 0x7F)
            {
                int shift = 0;
                while ((span[offset] & 0x80) != 0)
                {
                    value += (span[offset] & 0x7F) << shift;
                    offset++;
                    shift += 7;
                }
                value += span[offset] & 0x7F;
                offset++;
            }
            return value;
        }

        // ---------------- host rewrite policy ----------------

        [Fact]
        public void Policy_Rewrites_Authority_With_And_Without_Port()
        {
            var host = OverwrittenHost();
            var withPort = new List<H2Header> { new(":authority", "original.example:8443"), new(":path", "/") };
            Assert.True(HttpHeaderRewriter.OverrideHostHeader(withPort, host));
            Assert.Equal("127.0.0.1:8443", withPort[0].Value);

            var withoutPort = new List<H2Header> { new(":authority", "ORIGINAL.example") };
            Assert.True(HttpHeaderRewriter.OverrideHostHeader(withoutPort, host));
            Assert.Equal("127.0.0.1:8443", withoutPort[0].Value);
        }

        [Fact]
        public void Policy_Rewrites_Regular_Host_Header_Too()
        {
            var host = OverwrittenHost();
            var headers = new List<H2Header>
            {
                new(":authority", "original.example"),
                new("host", "original.example:443"),
                new(":path", "/")
            };
            Assert.True(HttpHeaderRewriter.OverrideHostHeader(headers, host));
            Assert.Equal("127.0.0.1:8443", headers[0].Value);
            Assert.Equal("127.0.0.1:8443", headers[1].Value);
        }

        [Fact]
        public void Policy_Leaves_Other_Hosts_Alon()
        {
            var host = OverwrittenHost();
            var headers = new List<H2Header> { new(":authority", "other.example"), new("host", "other.example") };
            Assert.False(HttpHeaderRewriter.OverrideHostHeader(headers, host));
            Assert.Equal("other.example", headers[0].Value);
            Assert.Equal("other.example", headers[1].Value);
        }
    }
}
