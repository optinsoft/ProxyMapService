using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using System.Buffers.Binary;
using System.Text;

namespace ProxyMapService.Proxy.Tunnels
{
    public class H2ToH1ResponseTunnel : HttpResponseTunnel
    {
        private const int WindowThreshold = 32 * 1024;
        private const int MaxHeaderBlock = 256 * 1024;

        private readonly Http2ClientConnection _conn;
        private readonly IHpackDecoder _hpack;

        private readonly H2FrameReader _reader = new();
        private readonly MemoryStream _block = new();

        private bool _inContinuation, _blockEndStream, _finalHeadersSent;
        private int _blockStream;
        private BodyMode _mode;
        private long _clRemaining;
        private int _pendingWindow;

        private bool _closeAfterResponse;

        public H2ToH1ResponseTunnel(
            SessionContext context,
            CountingStream source,
            CountingStream destination,
            TaskCompletionSource destinationReady,
            BytesReadCounter readCounter,
            BytesSendCounter sendCounter,
            TunnelState selfState,
            TunnelState otherTunnelState,
            Http2ClientConnection conn, 
            IHpackDecoder hpack)
            : base(
            context,
            source,
            destination,
            destinationReady,
            readCounter,
            sendCounter,
            selfState,
            otherTunnelState)
        {
            _conn = conn;
            _hpack = hpack;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _block.Dispose();
            base.Dispose(disposing);
        }

        protected override async Task ProcessAsync(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            _reader.Append(data.Span);
            while (_reader.TryRead(out var frame))
                await HandleFrameAsync(frame, token);
        }

        private async Task HandleFrameAsync(H2Frame f, CancellationToken ct)
        {
            if (_inContinuation && (f.Type != H2.Continuation || f.StreamId != _blockStream))
                throw new InvalidDataException("CONTINUATION expected");

            switch (f.Type)
            {
                case H2.Settings:
                    if ((f.Flags & H2.Ack) == 0)
                    {
                        _conn.OnPeerSettings(f.Payload);
                        await _conn.SendSettingsAckAsync(ct);
                    }
                    break;
                case H2.Ping:
                    if ((f.Flags & H2.Ack) == 0) await _conn.SendPingAckAsync(f.Payload, ct);
                    break;
                case H2.WindowUpdate:
                    if (f.Payload.Length != 4) throw new InvalidDataException("Bad WINDOW_UPDATE");
                    _conn.OnWindowUpdate(f.StreamId, (int)(BinaryPrimitives.ReadUInt32BigEndian(f.Payload) & 0x7FFFFFFF));
                    break;
                case H2.GoAway:
                    _conn.OnGoAway();
                    break;
                case H2.Headers:
                case H2.Continuation:
                    await OnHeaderFrameAsync(f, ct);
                    break;
                case H2.Data:
                    await OnDataAsync(f, ct);
                    break;
                case H2.RstStream:
                    await OnRstAsync(f, ct);
                    break;
                case H2.PushPromise:
                    throw new InvalidDataException("PUSH_PROMISE received while push is disabled");
            }
        }

        private bool AppendHeaderBlock(H2Frame f)
        {
            var p = f.Payload.AsSpan();
            if (f.Type == H2.Headers)
            {
                if (f.StreamId == 0) throw new InvalidDataException("HEADERS on stream 0");
                int pad = 0;
                if ((f.Flags & H2.Padded) != 0) { pad = p[0]; p = p[1..]; }
                if ((f.Flags & H2.PriorityFlag) != 0) p = p[5..];
                if (pad > p.Length) throw new InvalidDataException("Bad padding");
                p = p[..(p.Length - pad)];
                _block.SetLength(0);
                _blockStream = f.StreamId;
                _blockEndStream = (f.Flags & H2.EndStream) != 0;
            }
            _block.Write(p);
            if (_block.Length > MaxHeaderBlock) throw new InvalidDataException("Header block is too large");
            if ((f.Flags & H2.EndHeaders) == 0) { _inContinuation = true; return false; }
            _inContinuation = false;
            return true;
        }

        private List<H2Header> DecodeBlock()
            => _hpack.Decode(_block.GetBuffer().AsSpan(0, (int)_block.Length));

        private async Task OnHeaderFrameAsync(H2Frame f, CancellationToken ct)
        {
            if (!AppendHeaderBlock(f)) return;

            // always decode, even for a foreign stream: the HPACK dynamic table must stay in sync
            var headers = DecodeBlock();
            if (_blockStream != _conn.ActiveStream) return;

            if (!_finalHeadersSent)
            {
                bool close = !_conn.ActiveKeepAlive || _conn.GoAwayReceived;
                var head = Http1Http2Converter.BuildResponseHead(headers, _conn.ActiveMethod, close, _blockEndStream,
                    out int status, out var mode, out long cl);
                if (status < 200)
                {
                    await base.ProcessAsync(head, ct); // intermediate 1xx, same as in regular h1
                    return;
                }
                _closeAfterResponse = close;
                _finalHeadersSent = true;
                _mode = mode;
                _clRemaining = mode == BodyMode.ContentLength ? cl : 0;
                await base.ProcessAsync(head, ct);
                if (_blockEndStream) await EndOfMessageAsync(true, ct);
                return;
            }

            // trailers
            if (!_blockEndStream) throw new InvalidDataException("Trailers without END_STREAM");
            if (_mode == BodyMode.Chunked)
            {
                await base.ProcessAsync(Http1Http2Converter.BuildChunkedTrailer(headers), ct);
                await EndOfMessageAsync(false, ct);
            }
            else
            {
                await EndOfMessageAsync(true, ct); // for non-chunked, trailers are discarded
            }
        }

        private async Task OnDataAsync(H2Frame f, CancellationToken ct)
        {
            int frameLen = f.Payload.Length;
            int start = 0, len = frameLen;
            if ((f.Flags & H2.Padded) != 0)
            {
                int pad = f.Payload[0];
                start = 1;
                len = frameLen - 1 - pad;
                if (len < 0) throw new InvalidDataException("Bad padding");
            }
            bool end = (f.Flags & H2.EndStream) != 0;

            if (f.StreamId != _conn.ActiveStream || !_finalHeadersSent)
            {
                await _conn.SendWindowUpdateAsync(0, frameLen, ct); // foreign stream: only return the connection window
                return;
            }

            if (len > 0)
            {
                byte[] wire;
                if (_mode == BodyMode.Chunked)
                {
                    var hex = Encoding.ASCII.GetBytes(len.ToString("X"));
                    wire = new byte[hex.Length + 2 + len + 2];
                    hex.CopyTo(wire, 0);
                    wire[hex.Length] = (byte)'\r';
                    wire[hex.Length + 1] = (byte)'\n';
                    Buffer.BlockCopy(f.Payload, start, wire, hex.Length + 2, len);
                    wire[^2] = (byte)'\r';
                    wire[^1] = (byte)'\n';
                }
                else if (_mode == BodyMode.ContentLength)
                {
                    if (len > _clRemaining) throw new InvalidDataException("DATA exceeds content-length");
                    _clRemaining -= len;
                    wire = new byte[len];
                    Buffer.BlockCopy(f.Payload, start, wire, 0, len);
                }
                else throw new InvalidDataException("DATA for a response without body");

                await base.ProcessAsync(wire, ct); // the client has received it — only now do we return the window to the upstream
            }

            _pendingWindow += frameLen;
            if (_pendingWindow >= WindowThreshold || end)
            {
                await _conn.SendWindowUpdateAsync(0, _pendingWindow, ct);
                if (!end) await _conn.SendWindowUpdateAsync(f.StreamId, _pendingWindow, ct);
                _pendingWindow = 0;
            }

            if (end) await EndOfMessageAsync(true, ct);
        }

        private async Task OnRstAsync(H2Frame f, CancellationToken ct)
        {
            if (f.StreamId != _conn.ActiveStream) return;
            uint code = f.Payload.Length == 4 ? BinaryPrimitives.ReadUInt32BigEndian(f.Payload) : 0;
            bool headersSent = _finalHeadersSent;
            _finalHeadersSent = false;
            _mode = BodyMode.None;
            _conn.MarkResponseEnded(reset: true);
            if (!headersSent)
            {
                await HttpProto.HttpReplyBadGateway(_context, _context.RequestHeader, _destination,
                    $"Upstream reset the stream (HTTP/2 error {code})");
                throw new IOException("Upstream reset the stream before the response");
            }
            throw new IOException($"Upstream reset the stream mid-response (HTTP/2 error {code})");
        }

        private async Task EndOfMessageAsync(bool writeChunkTerminator, CancellationToken ct)
        {
            if (_mode == BodyMode.Chunked && writeChunkTerminator)
                await base.ProcessAsync("0\r\n\r\n"u8.ToArray(), ct);
            if (_mode == BodyMode.ContentLength && _clRemaining != 0)
                throw new InvalidDataException("Response shorter than content-length");
            _finalHeadersSent = false;
            _mode = BodyMode.None;
            _conn.MarkResponseEnded();
            if (_closeAfterResponse)
            {
                _closeAfterResponse = false;
                // RunAsync catches IOException silently; WhenAny in TunnelHandler will cancel the second tunnel and close the session
                throw new IOException("Closing the connection after the response (Connection: close / GOAWAY)");
            }
        }
    }
}
