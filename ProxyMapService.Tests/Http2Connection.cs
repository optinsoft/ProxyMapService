using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyMapService.Tests
{
    // ---------------------------------------------------------------
    // Minimal raw HTTP/2 client - only what the RFC 8441 test needs:
    // SETTINGS handshake, one CONNECT stream, HPACK-encoded headers,
    // DATA frames, and enough HPACK decoding to read the :status.
    // ---------------------------------------------------------------

    internal class Http2FrameType
    {
        public const byte Data = 0x0;
        public const byte Headers = 0x1;
        public const byte RstStream = 0x3;
        public const byte Settings = 0x4;
        public const byte Ping = 0x6;
        public const byte GoAway = 0x7;
    }

    internal class Http2Flags
    {
        public const byte None = 0x0;
        public const byte EndStream = 0x1;
        public const byte Ack = 0x1;
        public const byte EndHeaders = 0x4;
    }

    internal class Http2Connection
    {
        private static readonly byte[] Preface =
            Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");

        private readonly Stream _stream;
        private readonly CancellationToken _token;

        public Http2Connection(Stream stream, CancellationToken token)
        {
            _stream = stream;
            _token = token;
        }

        public async Task StartAsync()
        {
            await _stream.WriteAsync(Preface, _token);
            await WriteFrameAsync(Http2FrameType.Settings, Http2Flags.None, 0, Array.Empty<byte>());
            await _stream.FlushAsync(_token);

            // Wait for SETTINGS from the server (other frames may also arrive) and send an ACK.
            while (true)
            {
                var frame = await ReadFrameAsync();
                if (frame.Type == Http2FrameType.Settings && (frame.Flags & Http2Flags.Ack) == 0)
                {
                    await WriteFrameAsync(Http2FrameType.Settings, Http2Flags.Ack, 0, Array.Empty<byte>());
                    await _stream.FlushAsync(_token);
                    return;
                }
                await HandleIncomingAsync(frame);
            }
        }

        public async Task WriteAsync(byte[] frame)
        {
            await _stream.WriteAsync(frame, _token);
            await _stream.FlushAsync(_token);
        }

        public async Task<Dictionary<string, string>> WaitForHeadersAsync(uint streamId)
        {
            while (true)
            {
                var frame = await ReadFrameAsync();
                await HandleIncomingAsync(frame);

                if (frame.Type == Http2FrameType.Headers && frame.StreamId == streamId)
                    return DecodeHeaders(frame.Payload);

                if (frame.Type == Http2FrameType.RstStream && frame.StreamId == streamId)
                    throw new InvalidOperationException("Server reset the request stream");
            }
        }

        public async Task<byte[]> WaitForDataAsync(uint streamId)
        {
            while (true)
            {
                var frame = await ReadFrameAsync();
                await HandleIncomingAsync(frame);

                if (frame.Type == Http2FrameType.Data && frame.StreamId == streamId)
                    return frame.Payload;
            }
        }

        private async Task HandleIncomingAsync(Frame frame)
        {
            if (frame.Type == Http2FrameType.Settings && (frame.Flags & Http2Flags.Ack) == 0)
            {
                await WriteFrameAsync(Http2FrameType.Settings, Http2Flags.Ack, 0, Array.Empty<byte>());
                await _stream.FlushAsync(_token);
            }
            else if (frame.Type == Http2FrameType.Ping && (frame.Flags & Http2Flags.Ack) == 0)
            {
                await WriteFrameAsync(Http2FrameType.Ping, Http2Flags.Ack, 0, frame.Payload);
                await _stream.FlushAsync(_token);
            }
            else if (frame.Type == Http2FrameType.GoAway && frame.Payload.Length >= 8)
            {
                int code = ((frame.Payload[4] & 0x7F) << 24)
                         | (frame.Payload[5] << 16)
                         | (frame.Payload[6] << 8)
                         | frame.Payload[7];
                throw new InvalidOperationException($"Server sent GOAWAY, error code 0x{code:X}");
            }
        }

        private async Task<Frame> ReadFrameAsync()
        {
            var header = new byte[9];
            await _stream.ReadExactlyAsync(header, _token);

            int length = (header[0] << 16) | (header[1] << 8) | header[2];
            byte type = header[3];
            byte flags = header[4];
            uint streamId = (uint)(((header[5] & 0x7F) << 24)
                                 | (header[6] << 16)
                                 | (header[7] << 8)
                                 | header[8]);

            byte[] payload = length == 0 ? Array.Empty<byte>() : new byte[length];
            if (length > 0)
                await _stream.ReadExactlyAsync(payload, _token);

            return new Frame(type, flags, streamId, payload);
        }

        private ValueTask WriteFrameAsync(byte type, byte flags, uint streamId, byte[] payload) =>
            _stream.WriteAsync(BuildFrame(type, flags, streamId, payload), _token);

        public static byte[] BuildFrame(byte type, byte flags, uint streamId, byte[] payload)
        {
            var result = new byte[9 + payload.Length];
            result[0] = (byte)(payload.Length >> 16);
            result[1] = (byte)(payload.Length >> 8);
            result[2] = (byte)payload.Length;
            result[3] = type;
            result[4] = flags;
            result[5] = (byte)((streamId >> 24) & 0x7F);
            result[6] = (byte)(streamId >> 16);
            result[7] = (byte)(streamId >> 8);
            result[8] = (byte)streamId;
            if (payload.Length > 0)
                Buffer.BlockCopy(payload, 0, result, 9, payload.Length);
            return result;
        }

        // HPACK: encode each header as "literal without indexing, new name"
        // (prefix 0000, no Huffman). A dynamic table is not needed.
        public static byte[] EncodeHeaders((string Name, string Value)[] headers)
        {
            using var ms = new MemoryStream();
            foreach (var (name, value) in headers)
            {
                var nameBytes = Encoding.ASCII.GetBytes(name);
                var valueBytes = Encoding.ASCII.GetBytes(value);

                ms.WriteByte(0x00);
                ms.WriteByte((byte)nameBytes.Length);
                ms.Write(nameBytes);
                ms.WriteByte((byte)valueBytes.Length);
                ms.Write(valueBytes);
            }
            return ms.ToArray();
        }

        public static Dictionary<string, string> DecodeHeaders(byte[] data)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int pos = 0;

            while (pos < data.Length)
            {
                byte b = data[pos];

                if ((b & 0x80) != 0)
                {
                    // Indexed header field (static table).
                    int index = ReadInteger(data, ref pos, 7);
                    if (StaticTable.TryGetValue(index, out var entry))
                        headers[entry.Name] = entry.Value;
                }
                else if ((b & 0xC0) == 0x40)
                {
                    // Literal with incremental indexing.
                    int index = ReadInteger(data, ref pos, 6);
                    var name = index > 0 ? StaticTable[index].Name : ReadString(data, ref pos);
                    var value = ReadString(data, ref pos);
                    headers[name] = value;
                }
                else if ((b & 0xF0) == 0x00 || (b & 0xF0) == 0x10)
                {
                    // Literal without indexing / never indexed.
                    int index = ReadInteger(data, ref pos, 4);
                    var name = index > 0 ? StaticTable[index].Name : ReadString(data, ref pos);
                    var value = ReadString(data, ref pos);
                    headers[name] = value;
                }
                else if ((b & 0xE0) == 0x20)
                {
                    // Dynamic table size update - ignore.
                    ReadInteger(data, ref pos, 5);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported HPACK prefix 0x{b:X2}");
                }
            }

            return headers;
        }

        private static int ReadInteger(byte[] data, ref int pos, int prefixBits)
        {
            int mask = (1 << prefixBits) - 1;
            int value = data[pos++] & mask;

            if (value == mask)
            {
                int shift = 0, b;
                do
                {
                    b = data[pos++];
                    value += (b & 0x7F) << shift;
                    shift += 7;
                } while ((b & 0x80) != 0);
            }
            return value;
        }

        private static string ReadString(byte[] data, ref int pos)
        {
            bool huffman = (data[pos] & 0x80) != 0;
            int length = ReadInteger(data, ref pos, 7);

            var bytes = new byte[length];
            Buffer.BlockCopy(data, pos, bytes, 0, length);
            pos += length;

            // In the test, there is no need to decode Huffman-encoded values:
            // :status arrives as an indexed field from the static table.
            return huffman ? "<huffman>" : Encoding.ASCII.GetString(bytes);
        }

        // Full HPACK static table (RFC 7541, Appendix A). Entries 15-61 have an empty value;
        // the server uses them as name references inside "literal" representations
        // (e.g. 0x61 = "literal with incremental indexing, name index 33 = date").
        private static readonly Dictionary<int, (string Name, string Value)> StaticTable = new()
        {
            [1] = (":authority", ""),
            [2] = (":method", "GET"),
            [3] = (":method", "POST"),
            [4] = (":path", "/"),
            [5] = (":path", "/index.html"),
            [6] = (":scheme", "http"),
            [7] = (":scheme", "https"),
            [8] = (":status", "200"),
            [9] = (":status", "204"),
            [10] = (":status", "206"),
            [11] = (":status", "304"),
            [12] = (":status", "400"),
            [13] = (":status", "404"),
            [14] = (":status", "500"),
            [15] = ("accept-charset", ""),
            [16] = ("accept-encoding", "gzip, deflate"),
            [17] = ("accept-language", ""),
            [18] = ("accept-ranges", ""),
            [19] = ("accept", ""),
            [20] = ("access-control-allow-origin", ""),
            [21] = ("age", ""),
            [22] = ("allow", ""),
            [23] = ("authorization", ""),
            [24] = ("cache-control", ""),
            [25] = ("content-disposition", ""),
            [26] = ("content-encoding", ""),
            [27] = ("content-language", ""),
            [28] = ("content-length", ""),
            [29] = ("content-location", ""),
            [30] = ("content-range", ""),
            [31] = ("content-type", ""),
            [32] = ("cookie", ""),
            [33] = ("date", ""),
            [34] = ("etag", ""),
            [35] = ("expect", ""),
            [36] = ("expires", ""),
            [37] = ("from", ""),
            [38] = ("host", ""),
            [39] = ("if-match", ""),
            [40] = ("if-modified-since", ""),
            [41] = ("if-none-match", ""),
            [42] = ("if-range", ""),
            [43] = ("if-unmodified-since", ""),
            [44] = ("last-modified", ""),
            [45] = ("link", ""),
            [46] = ("location", ""),
            [47] = ("max-forwards", ""),
            [48] = ("proxy-authenticate", ""),
            [49] = ("proxy-authorization", ""),
            [50] = ("range", ""),
            [51] = ("referer", ""),
            [52] = ("refresh", ""),
            [53] = ("retry-after", ""),
            [54] = ("server", ""),
            [55] = ("set-cookie", ""),
            [56] = ("strict-transport-security", ""),
            [57] = ("transfer-encoding", ""),
            [58] = ("user-agent", ""),
            [59] = ("vary", ""),
            [60] = ("via", ""),
            [61] = ("www-authenticate", ""),
        };

        private readonly record struct Frame(byte Type, byte Flags, uint StreamId, byte[] Payload);
    }
}
