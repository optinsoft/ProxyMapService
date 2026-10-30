using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;

namespace ProxyMapService.Proxy.Http2
{    
    /// <summary>
    /// HTTP/2 server side (we are the server, as in FileRequestHandler): preface, SETTINGS, request HPACK,
    /// multiplexing with a separate task per request, flow control when sending the body.
    /// The handler is invoked synchronously from the read loop (before the first await), so request order is preserved.
    /// </summary>
    public sealed class Http2ServerConnection
    {
        private const int MaxConcurrentStreams = 100;
        private const int MaxHeaderBlock = 64 * 1024;

        private readonly Stream _stream;
        private readonly IHpackDecoder _hpack;
        private readonly IHpackEncoder _encoder;
        private readonly Func<Http2ServerStream, Task> _handler;
        private readonly ILogger? _logger;

        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly object _sync = new();
        private TaskCompletionSource _signal = NewTcs();
        private readonly Dictionary<int, Http2ServerStream> _streams = new();
        private readonly List<Task> _tasks = new();

        private CancellationToken _ct;
        private long _connWindow = 65535;
        private int _peerInitialWindow = 65535;
        private volatile int _peerMaxFrame = 16384;

        // read-loop state (used only by it)
        private readonly MemoryStream _block = new();
        private bool _inContinuation, _blockEndStream;
        private int _blockStream, _lastStreamId;
        private bool _goAway;

        public Http2ServerConnection(Stream stream, IHpackDecoder hpack, Func<Http2ServerStream, Task> handler,
            ILogger? logger = null, IHpackEncoder? encoder = null)
        {
            _stream = stream;
            _hpack = hpack;
            _encoder = encoder ?? new HpackEncoderAdapter();
            _handler = handler;
            _logger = logger;
        }

        private static TaskCompletionSource NewTcs() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private void Pulse()
        {
            var t = _signal;
            _signal = NewTcs();
            t.TrySetResult();
        }

        // ---------------- main loop ----------------

        /// <param name="initial">Bytes already read from the stream (e.g., when detecting h2c by preface).</param>
        public async Task RunAsync(ReadOnlyMemory<byte> initial, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _ct = cts.Token;

            var reader = new H2FrameReader();
            var buffer = new byte[16 * 1024];
            int prefaceLeft = H2.Preface.Length;
            ReadOnlyMemory<byte> data = initial;

            try
            {
                await WriteSettingsAsync();
                while (true)
                {
                    if (data.IsEmpty)
                    {
                        int n = await _stream.ReadAsync(buffer.AsMemory(), _ct);
                        if (n == 0) break;
                        data = buffer.AsMemory(0, n);
                    }
                    if (prefaceLeft > 0) ConsumePreface(ref data, ref prefaceLeft);
                    if (!data.IsEmpty)
                    {
                        reader.Append(data.Span);
                        data = default;
                        while (reader.TryRead(out var frame))
                            await HandleFrameAsync(frame);
                    }
                    if (_goAway && ActiveStreams() == 0) break; // the client asked to close, and there is no more work
                }
            }
            catch (H2ConnectionException ex)
            {
                Http2Log.ServerConnectionClosed(_logger, ex.Message);
                await TrySendGoAwayAsync(ex.Code);
            }
            catch (InvalidDataException ex) // frame too large from the frame reader
            {
                Http2Log.ServerConnectionClosed(_logger, ex.Message);
                await TrySendGoAwayAsync(H2Error.FrameSizeError);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // the client is gone or the session is closing
            }

            cts.Cancel(); // the remaining handlers are cancelled
            Task[] running;
            lock (_tasks) running = _tasks.ToArray();
            try { await Task.WhenAll(running); }
            catch { /* RunHandlerAsync doesn't throw, but we don't let it crash on close */ }
            _block.Dispose();
        }

        private int ActiveStreams()
        {
            lock (_sync) return _streams.Count;
        }

        private static void ConsumePreface(ref ReadOnlyMemory<byte> data, ref int prefaceLeft)
        {
            int n = Math.Min(prefaceLeft, data.Length);
            var expected = H2.Preface.AsSpan(H2.Preface.Length - prefaceLeft, n);
            if (!data.Span[..n].SequenceEqual(expected))
                throw new H2ConnectionException(H2Error.ProtocolError, "Invalid HTTP/2 client preface");
            prefaceLeft -= n;
            data = data[n..];
        }

        private Task WriteSettingsAsync()
        {
            byte[] settings = [0, 3, 0, 0, 0, MaxConcurrentStreams,   // MAX_CONCURRENT_STREAMS
                               0, 6, 0, 1, 0, 0];                     // MAX_HEADER_LIST_SIZE = 64 KiB
            return WriteFrameAsync(H2.Settings, 0, 0, settings, _ct);
        }

        private async Task TrySendGoAwayAsync(uint code)
        {
            try
            {
                var p = new byte[8];
                BinaryPrimitives.WriteInt32BigEndian(p, _lastStreamId);
                BinaryPrimitives.WriteUInt32BigEndian(p.AsSpan(4), code);
                await WriteFrameAsync(H2.GoAway, 0, 0, p, _ct);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
            }
        }

        // ---------------- incoming frames ----------------

        private async Task HandleFrameAsync(H2Frame f)
        {
            if (_inContinuation)
            {
                if (f.Type != H2.Continuation || f.StreamId != _blockStream)
                    throw new H2ConnectionException(H2Error.ProtocolError, "CONTINUATION expected");
            }
            else if (f.Type == H2.Continuation)
            {
                throw new H2ConnectionException(H2Error.ProtocolError, "Unexpected CONTINUATION");
            }

            switch (f.Type)
            {
                case H2.Settings:
                    if ((f.Flags & H2.Ack) == 0)
                    {
                        ApplyPeerSettings(f);
                        await WriteFrameAsync(H2.Settings, H2.Ack, 0, ReadOnlyMemory<byte>.Empty, _ct);
                    }
                    break;
                case H2.Ping:
                    if (f.StreamId != 0 || f.Payload.Length != 8)
                        throw new H2ConnectionException(H2Error.ProtocolError, "Bad PING");
                    if ((f.Flags & H2.Ack) == 0) await WriteFrameAsync(H2.Ping, H2.Ack, 0, f.Payload, _ct);
                    break;
                case H2.WindowUpdate:
                    OnWindowUpdate(f);
                    break;
                case H2.Headers:
                case H2.Continuation:
                    if (AppendHeaderBlock(f))
                    {
                        var headers = DecodeBlock();
                        await OnRequestHeadersAsync(_blockStream, headers, _blockEndStream);
                    }
                    break;
                case H2.Data:
                    await OnDataAsync(f);
                    break;
                case H2.RstStream:
                    OnRst(f);
                    break;
                case H2.GoAway:
                    _goAway = true;
                    break;
                case H2.PushPromise:
                    throw new H2ConnectionException(H2Error.ProtocolError, "PUSH_PROMISE from the client");
                    // PRIORITY and unknown types are ignored
            }
        }

        private void ApplyPeerSettings(H2Frame f)
        {
            var p = f.Payload;
            if (f.StreamId != 0 || p.Length % 6 != 0)
                throw new H2ConnectionException(H2Error.FrameSizeError, "Bad SETTINGS");
            lock (_sync)
            {
                for (int i = 0; i < p.Length; i += 6)
                {
                    int id = (p[i] << 8) | p[i + 1];
                    uint val = BinaryPrimitives.ReadUInt32BigEndian(p.AsSpan(i + 2, 4));
                    if (id == 4) // INITIAL_WINDOW_SIZE
                    {
                        if (val > int.MaxValue) throw new H2ConnectionException(H2Error.FlowControlError, "Bad INITIAL_WINDOW_SIZE");
                        long delta = (long)val - _peerInitialWindow;
                        _peerInitialWindow = (int)val;
                        foreach (var s in _streams.Values) s.Window += delta;
                    }
                    else if (id == 5) // MAX_FRAME_SIZE
                    {
                        if (val is < 16384 or > 16777215) throw new H2ConnectionException(H2Error.ProtocolError, "Bad MAX_FRAME_SIZE");
                        _peerMaxFrame = (int)val;
                    }
                    // HEADER_TABLE_SIZE doesn't concern us: the encoder has no dynamic table
                }
                Pulse();
            }
        }

        private void OnWindowUpdate(H2Frame f)
        {
            if (f.Payload.Length != 4) throw new H2ConnectionException(H2Error.FrameSizeError, "Bad WINDOW_UPDATE");
            long inc = BinaryPrimitives.ReadUInt32BigEndian(f.Payload) & 0x7FFFFFFF;
            if (inc == 0)
            {
                if (f.StreamId == 0) throw new H2ConnectionException(H2Error.ProtocolError, "Zero WINDOW_UPDATE");
                return;
            }
            lock (_sync)
            {
                if (f.StreamId == 0)
                {
                    _connWindow += inc;
                    if (_connWindow > int.MaxValue) throw new H2ConnectionException(H2Error.FlowControlError, "Window overflow");
                }
                else if (_streams.TryGetValue(f.StreamId, out var s))
                {
                    s.Window = Math.Min(s.Window + inc, int.MaxValue);
                }
                Pulse();
            }
        }

        private void OnRst(H2Frame f)
        {
            if (f.StreamId == 0 || f.Payload.Length != 4) throw new H2ConnectionException(H2Error.ProtocolError, "Bad RST_STREAM");
            Http2ServerStream? s;
            lock (_sync)
            {
                if (!_streams.Remove(f.StreamId, out s)) return;
            }
            s.Cancel(); // the handler and the sends waiting for window will be cancelled
        }

        private async Task OnDataAsync(H2Frame f)
        {
            if (f.StreamId == 0) throw new H2ConnectionException(H2Error.ProtocolError, "DATA on stream 0");
            int len = f.Payload.Length; // padding is also included in flow control
            bool end = (f.Flags & H2.EndStream) != 0;
            Http2ServerStream? s;
            lock (_sync) _streams.TryGetValue(f.StreamId, out s);

            // the request body is discarded, so we return the windows immediately
            if (len > 0)
            {
                await WriteWindowUpdateAsync(0, len);
                if (s != null && !end) await WriteWindowUpdateAsync(f.StreamId, len);
            }
            if (s != null && end) s.RemoteEnded = true;
        }

        private bool AppendHeaderBlock(H2Frame f)
        {
            var p = f.Payload.AsSpan();
            if (f.Type == H2.Headers)
            {
                if (f.StreamId == 0) throw new H2ConnectionException(H2Error.ProtocolError, "HEADERS on stream 0");
                int pad = 0;
                if ((f.Flags & H2.Padded) != 0)
                {
                    if (p.IsEmpty) throw new H2ConnectionException(H2Error.ProtocolError, "Bad padding");
                    pad = p[0];
                    p = p[1..];
                }
                if ((f.Flags & H2.PriorityFlag) != 0)
                {
                    if (p.Length < 5) throw new H2ConnectionException(H2Error.FrameSizeError, "Bad HEADERS priority");
                    p = p[5..];
                }
                if (pad > p.Length) throw new H2ConnectionException(H2Error.ProtocolError, "Bad padding");
                p = p[..(p.Length - pad)];
                _block.SetLength(0);
                _blockStream = f.StreamId;
                _blockEndStream = (f.Flags & H2.EndStream) != 0;
            }
            _block.Write(p);
            if (_block.Length > MaxHeaderBlock)
                throw new H2ConnectionException(H2Error.CompressionError, "Header block is too large");
            if ((f.Flags & H2.EndHeaders) == 0)
            {
                _inContinuation = true;
                return false;
            }
            _inContinuation = false;
            return true;
        }

        private List<H2Header> DecodeBlock()
        {
            try
            {
                return _hpack.Decode(_block.GetBuffer().AsSpan(0, (int)_block.Length));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new H2ConnectionException(H2Error.CompressionError, "HPACK decoding failed: " + ex.Message);
            }
        }

        private async Task OnRequestHeadersAsync(int id, List<H2Header> headers, bool endStream)
        {
            Http2ServerStream? existing;
            lock (_sync) _streams.TryGetValue(id, out existing);
            if (existing != null)
            {
                // request trailers: we don't need their contents
                if (endStream) existing.RemoteEnded = true;
                return;
            }

            if ((id & 1) == 0 || id <= _lastStreamId)
                throw new H2ConnectionException(H2Error.ProtocolError, "Invalid stream id " + id);
            _lastStreamId = id;

            bool refuse;
            lock (_sync) refuse = _goAway || _streams.Count >= MaxConcurrentStreams;
            if (refuse)
            {
                await WriteRstAsync(id, H2Error.RefusedStream);
                return;
            }

            Http2ServerStream stream;
            lock (_sync)
            {
                stream = new Http2ServerStream(this, id, headers, endStream, _peerInitialWindow, _ct);
                _streams[id] = stream;
            }

            var task = RunHandlerAsync(stream); // the synchronous part of the handler runs here, in request order
            lock (_tasks) _tasks.Add(task);
            _ = task.ContinueWith(t => { lock (_tasks) _tasks.Remove(t); }, TaskScheduler.Default);
        }

        private async Task RunHandlerAsync(Http2ServerStream s)
        {
            try
            {
                await _handler(s);
            }
            catch (OperationCanceledException)
            {
                // the client reset the stream or the connection is closed
            }
            catch (Exception ex)
            {
                Http2Log.ServerHandlerFailed(_logger, s.StreamId, ex.GetType().Name, ex.Message);
                if (!s.ResponseStarted && !s.Token.IsCancellationRequested)
                {
                    try { await SendHeadersAsync(s, 500, [("content-length", "0")], true); }
                    catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException) { }
                }
            }
            finally
            {
                await FinishStreamAsync(s);
            }
        }

        private async Task FinishStreamAsync(Http2ServerStream s)
        {
            try
            {
                if (!s.Token.IsCancellationRequested)
                {
                    if (!s.ResponseEnded) await WriteRstAsync(s.StreamId, H2Error.InternalError);   // the handler did not finish the response
                    else if (!s.RemoteEnded) await WriteRstAsync(s.StreamId, H2Error.NoError);      // RFC 9113 8.1: response before the request body
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
            }
            finally
            {
                lock (_sync) _streams.Remove(s.StreamId);
                s.DisposeToken();
            }
        }

        // ---------------- sending ----------------

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

        private Task WriteWindowUpdateAsync(int streamId, int increment)
        {
            var p = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(p, increment);
            return WriteFrameAsync(H2.WindowUpdate, 0, streamId, p, _ct);
        }

        private Task WriteRstAsync(int streamId, uint errorCode)
        {
            var p = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(p, errorCode);
            return WriteFrameAsync(H2.RstStream, 0, streamId, p, _ct);
        }

        internal async Task SendHeadersAsync(Http2ServerStream s, int status, IReadOnlyList<(string Name, string Value)> headers, bool endStream)
        {
            if (s.ResponseEnded) throw new InvalidOperationException("The response has already been completed");

            var list = new List<H2Header>(headers.Count + 1)
            {
                new(":status", status.ToString(CultureInfo.InvariantCulture))
            };
            foreach (var (name, value) in headers)
                list.Add(new(name.ToLowerInvariant(), value));

            s.ResponseStarted = true;
            if (endStream) s.ResponseEnded = true;

            var block = _encoder.Encode(list).AsMemory();
            await _writeLock.WaitAsync(s.Token); // HEADERS + CONTINUATION must not be interleaved with other frames
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
                    await WriteFrameLockedAsync(first ? H2.Headers : H2.Continuation, flags, s.StreamId, part, s.Token);
                    first = false;
                } while (!block.IsEmpty);
            }
            finally { _writeLock.Release(); }
        }

        internal async Task SendDataAsync(Http2ServerStream s, ReadOnlyMemory<byte> data, bool endStream)
        {
            if (s.ResponseEnded) throw new InvalidOperationException("The response has already been completed");
            if (!s.ResponseStarted) throw new InvalidOperationException("Send the headers first");

            if (data.IsEmpty)
            {
                if (endStream)
                {
                    s.ResponseEnded = true;
                    await WriteFrameAsync(H2.Data, H2.EndStream, s.StreamId, ReadOnlyMemory<byte>.Empty, s.Token);
                }
                return;
            }

            while (!data.IsEmpty)
            {
                int n = await AcquireWindowAsync(s, data.Length);
                bool last = endStream && n == data.Length;
                if (last) s.ResponseEnded = true;
                await WriteFrameAsync(H2.Data, last ? H2.EndStream : (byte)0, s.StreamId, data[..n], s.Token);
                data = data[n..];
            }
        }

        private async Task<int> AcquireWindowAsync(Http2ServerStream s, int want)
        {
            while (true)
            {
                Task wait;
                lock (_sync)
                {
                    s.Token.ThrowIfCancellationRequested();
                    long n = Math.Min(Math.Min(want, _peerMaxFrame), Math.Min(_connWindow, s.Window));
                    if (n > 0)
                    {
                        _connWindow -= n;
                        s.Window -= n;
                        return (int)n;
                    }
                    wait = _signal.Task;
                }
                await wait.WaitAsync(s.Token);
            }
        }
    }

    internal static partial class Http2Log
    {
        [LoggerMessage(EventId = 1506, Level = LogLevel.Warning,
            Message = "[h2 stream {StreamId}] request handler failed: {ExceptionName}: {ErrorMessage}")]
        public static partial void ServerHandlerFailed(ILogger? logger, int streamId, string exceptionName, string errorMessage);

        [LoggerMessage(EventId = 1507, Level = LogLevel.Debug,
            Message = "[h2] server connection closed: {Reason}")]
        public static partial void ServerConnectionClosed(ILogger? logger, string reason);
    }
}
