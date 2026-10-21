using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace ProxyMapService.Proxy.Http2
{
    public static class H2
    {
        public const byte Data = 0, Headers = 1, Priority = 2, RstStream = 3, Settings = 4,
            PushPromise = 5, Ping = 6, GoAway = 7, WindowUpdate = 8, Continuation = 9;
        public const byte EndStream = 0x1, Ack = 0x1, EndHeaders = 0x4, Padded = 0x8, PriorityFlag = 0x20;
        public static readonly byte[] Preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();
    }

    public readonly record struct H2Header(string Name, string Value);
    public readonly record struct H2Frame(byte Type, byte Flags, int StreamId, byte[] Payload);

    /// <summary>Wrapper over the HPACK decoder (copied from dotnet/runtime). Must only be called from the reading tunnel.</summary>
    public interface IHpackDecoder
    {
        List<H2Header> Decode(ReadOnlySpan<byte> block);
    }

    /// <summary>Accumulates bytes and yields whole frames.</summary>
    public sealed class H2FrameReader
    {
        private byte[] _buf = new byte[32 * 1024];
        private int _start, _end;
        private const int MaxFrameSize = 16384; // our SETTINGS_MAX_FRAME_SIZE (default)

        public void Append(ReadOnlySpan<byte> data)
        {
            if (_buf.Length - _end < data.Length)
            {
                int len = _end - _start;
                if (_buf.Length - len < data.Length)
                    Array.Resize(ref _buf, Math.Max(_buf.Length * 2, len + data.Length));
                Buffer.BlockCopy(_buf, _start, _buf, 0, len);
                _start = 0;
                _end = len;
            }
            data.CopyTo(_buf.AsSpan(_end));
            _end += data.Length;
        }

        public bool TryRead(out H2Frame frame)
        {
            frame = default;
            int avail = _end - _start;
            if (avail < 9) return false;
            var s = _buf.AsSpan(_start, avail);
            int len = (s[0] << 16) | (s[1] << 8) | s[2];
            if (len > MaxFrameSize) throw new InvalidDataException("HTTP/2 frame is too large");
            if (avail < 9 + len) return false;
            frame = new H2Frame(s[3], s[4], BinaryPrimitives.ReadInt32BigEndian(s.Slice(5, 4)) & 0x7FFFFFFF,
                s.Slice(9, len).ToArray());
            _start += 9 + len;
            if (_start == _end) _start = _end = 0;
            return true;
        }
    }

    /// <summary>Minimal HPACK encoder: no dynamic table and no Huffman (this is valid).</summary>
    public static class HpackEncoder
    {
        public static byte[] Encode(IReadOnlyList<H2Header> headers)
        {
            var w = new ArrayBufferWriter<byte>(256);
            foreach (var (name, value) in headers)
            {
                bool sensitive = name is "authorization" or "cookie" or "proxy-authorization" or "set-cookie";
                Put(w, sensitive ? (byte)0x10 : (byte)0x00); // literal never-indexed / without indexing, new name
                PutString(w, name);
                PutString(w, value);
            }
            return w.WrittenSpan.ToArray();
        }

        private static void Put(ArrayBufferWriter<byte> w, byte b)
        {
            w.GetSpan(1)[0] = b;
            w.Advance(1);
        }

        private static void PutString(ArrayBufferWriter<byte> w, string s)
        {
            var bytes = Encoding.Latin1.GetBytes(s);
            PutInt(w, bytes.Length, 7, 0x00); // H = 0
            w.Write(bytes);
        }

        private static void PutInt(ArrayBufferWriter<byte> w, int value, int prefixBits, byte flags)
        {
            int max = (1 << prefixBits) - 1;
            if (value < max) { Put(w, (byte)(flags | value)); return; }
            Put(w, (byte)(flags | max));
            value -= max;
            while (value >= 128) { Put(w, (byte)((value & 0x7F) | 0x80)); value >>= 7; }
            Put(w, (byte)value);
        }
    }

    /// <summary>
    /// Client side of a single HTTP/2 connection to the upstream. One active request at a time
    /// (the client speaks HTTP/1.1, so pipelining is serialized). The sole frame writer.
    /// </summary>
    public sealed class Http2Connection
    {
        private const int OurStreamWindow = 1 << 20;

        private readonly Stream _stream;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly object _sync = new();
        private TaskCompletionSource _signal = NewTcs();

        private long _connWindow = 65535;
        private long _streamWindow;
        private int _peerInitialWindow = 65535;
        private volatile int _peerMaxFrame = 16384;
        private int _nextStreamId = 1;
        private int _activeStream;
        private bool _requestEnded, _responseEnded, _goAway;

        public Http2Connection(Stream stream) => _stream = stream;

        public int ActiveStream { get { lock (_sync) return _activeStream; } }
        public string? ActiveMethod { get; private set; }
        public bool ActiveKeepAlive { get; private set; } = true;
        public bool GoAwayReceived { get { lock (_sync) return _goAway; } }

        private static TaskCompletionSource NewTcs() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private void Pulse()
        {
            var t = _signal;
            _signal = NewTcs();
            t.TrySetResult();
        }

        // ---------- writing ----------

        private async Task WriteFrameLockedAsync(byte type, byte flags, int streamId, ReadOnlyMemory<byte> payload, CancellationToken ct)
        {
            var buf = ArrayPool<byte>.Shared.Rent(9 + payload.Length);
            try
            {
                buf[0] = (byte)(payload.Length >> 16);
                buf[1] = (byte)(payload.Length >> 8);
                buf[2] = (byte)payload.Length;
                buf[3] = type;
                buf[4] = flags;
                BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(5, 4), streamId);
                payload.CopyTo(buf.AsMemory(9));
                await _stream.WriteAsync(buf.AsMemory(0, 9 + payload.Length), ct);
            }
            finally { ArrayPool<byte>.Shared.Return(buf); }
        }

        private async Task WriteFrameAsync(byte type, byte flags, int streamId, ReadOnlyMemory<byte> payload, CancellationToken ct)
        {
            await _writeLock.WaitAsync(ct);
            try { await WriteFrameLockedAsync(type, flags, streamId, payload, ct); }
            finally { _writeLock.Release(); }
        }

        /// <summary>Preface + SETTINGS (push disabled, large stream window) + connection window extension.</summary>
        public async Task StartAsync(CancellationToken ct)
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                await _stream.WriteAsync(H2.Preface, ct);
                byte[] settings = [0, 2, 0, 0, 0, 0,            // ENABLE_PUSH = 0
                                   0, 4, 0x00, 0x10, 0x00, 0x00]; // INITIAL_WINDOW_SIZE = 1 MiB
                await WriteFrameLockedAsync(H2.Settings, 0, 0, settings, ct);
                var inc = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(inc, OurStreamWindow - 65535);
                await WriteFrameLockedAsync(H2.WindowUpdate, 0, 0, inc, ct);
            }
            finally { _writeLock.Release(); }
        }

        private async Task WriteHeaderBlockAsync(int id, ReadOnlyMemory<byte> block, bool endStream, CancellationToken ct)
        {
            await _writeLock.WaitAsync(ct); // HEADERS + CONTINUATION must not be interrupted by other frames
            try
            {
                int max = _peerMaxFrame;
                bool first = true;
                do
                {
                    var part = block.Length > max ? block[..max] : block;
                    block = block[part.Length..];
                    byte flags = 0;
                    if (block.IsEmpty) flags |= H2.EndHeaders;
                    if (first && endStream) flags |= H2.EndStream;
                    await WriteFrameLockedAsync(first ? H2.Headers : H2.Continuation, flags, id, part, ct);
                    first = false;
                } while (!block.IsEmpty);
            }
            finally { _writeLock.Release(); }
        }

        /// <summary>Waits for the previous exchange to complete, opens a stream and sends HEADERS.</summary>
        public async Task<int> BeginRequestAsync(string method, bool keepAlive, IReadOnlyList<H2Header> headers, bool endStream, CancellationToken ct)
        {
            int id = 0;
            while (true)
            {
                Task wait;
                lock (_sync)
                {
                    if (_goAway) throw new IOException("Upstream sent GOAWAY");
                    if (_activeStream == 0)
                    {
                        id = _nextStreamId;
                        _nextStreamId += 2;
                        _activeStream = id;
                        _streamWindow = _peerInitialWindow;
                        _requestEnded = _responseEnded = false;
                        ActiveMethod = method;
                        ActiveKeepAlive = keepAlive;
                        break;
                    }
                    wait = _signal.Task;
                }
                await wait.WaitAsync(ct);
            }

            await WriteHeaderBlockAsync(id, HpackEncoder.Encode(headers), endStream, ct);
            if (endStream) MarkRequestEnded();
            return id;
        }

        public async Task SendDataAsync(int streamId, ReadOnlyMemory<byte> data, bool endStream, CancellationToken ct)
        {
            if (data.IsEmpty)
            {
                if (endStream) await WriteFrameAsync(H2.Data, H2.EndStream, streamId, ReadOnlyMemory<byte>.Empty, ct);
                return;
            }
            while (!data.IsEmpty)
            {
                int n = await AcquireWindowAsync(streamId, data.Length, ct);
                bool last = endStream && n == data.Length;
                await WriteFrameAsync(H2.Data, last ? H2.EndStream : (byte)0, streamId, data[..n], ct);
                data = data[n..];
            }
        }

        public Task SendTrailersAsync(int streamId, IReadOnlyList<H2Header> trailers, CancellationToken ct)
            => WriteHeaderBlockAsync(streamId, HpackEncoder.Encode(trailers), true, ct);

        private async Task<int> AcquireWindowAsync(int streamId, int want, CancellationToken ct)
        {
            while (true)
            {
                Task wait;
                lock (_sync)
                {
                    if (_activeStream != streamId) throw new IOException("HTTP/2 stream is closed");
                    long n = Math.Min(Math.Min(want, _peerMaxFrame), Math.Min(_connWindow, _streamWindow));
                    if (n > 0)
                    {
                        _connWindow -= n;
                        _streamWindow -= n;
                        return (int)n;
                    }
                    wait = _signal.Task;
                }
                await wait.WaitAsync(ct);
            }
        }

        public Task SendSettingsAckAsync(CancellationToken ct)
            => WriteFrameAsync(H2.Settings, H2.Ack, 0, ReadOnlyMemory<byte>.Empty, ct);

        public Task SendPingAckAsync(byte[] payload, CancellationToken ct)
            => WriteFrameAsync(H2.Ping, H2.Ack, 0, payload, ct);

        public Task SendWindowUpdateAsync(int streamId, int increment, CancellationToken ct)
        {
            var p = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(p, increment);
            return WriteFrameAsync(H2.WindowUpdate, 0, streamId, p, ct);
        }

        public Task SendRstAsync(int streamId, uint errorCode, CancellationToken ct)
        {
            var p = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(p, errorCode);
            return WriteFrameAsync(H2.RstStream, 0, streamId, p, ct);
        }

        // ---------- events from the reading tunnel ----------

        public void OnPeerSettings(byte[] payload)
        {
            lock (_sync)
            {
                for (int i = 0; i + 6 <= payload.Length; i += 6)
                {
                    int id = (payload[i] << 8) | payload[i + 1];
                    uint val = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(i + 2, 4));
                    if (id == 4)
                    {
                        if (val > int.MaxValue) throw new InvalidDataException("Bad INITIAL_WINDOW_SIZE");
                        _streamWindow += (long)val - _peerInitialWindow;
                        _peerInitialWindow = (int)val;
                    }
                    else if (id == 5)
                    {
                        if (val is < 16384 or > 16777215) throw new InvalidDataException("Bad MAX_FRAME_SIZE");
                        _peerMaxFrame = (int)val;
                    }
                }
                Pulse();
            }
        }

        public void OnWindowUpdate(int streamId, int increment)
        {
            lock (_sync)
            {
                if (streamId == 0) _connWindow += increment;
                else if (streamId == _activeStream) _streamWindow += increment;
                Pulse();
            }
        }

        public void OnGoAway() { lock (_sync) { _goAway = true; Pulse(); } }

        public void MarkRequestEnded()
        {
            lock (_sync) { _requestEnded = true; if (_responseEnded) Clear(); }
        }

        public void MarkResponseEnded(bool reset = false)
        {
            lock (_sync) { _responseEnded = true; if (_requestEnded || reset) Clear(); }
        }

        private void Clear()
        {
            _activeStream = 0;
            ActiveMethod = null;
            _requestEnded = _responseEnded = false;
            Pulse();
        }
    }
}
