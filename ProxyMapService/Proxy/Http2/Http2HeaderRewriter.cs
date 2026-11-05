using System.Buffers;
using System.Buffers.Binary;

namespace ProxyMapService.Proxy.Http2
{
    /// <summary>
    /// Active HTTP/2 transformation of the client → server direction, used when requests must be modified
    /// before they reach the server (e.g. host override).
    /// Every client header block (request headers and trailers) is decoded with a decoder that follows the
    /// client's HPACK state, passed to the <paramref name="rewrite"/> policy and re-encoded with the
    /// stateless literal-only encoder, so the server never sees the client's original HPACK encoding.
    /// The connection preface and all other frames are passed through unchanged.
    /// When the policy changes a block, <paramref name="onRewritten"/> is invoked with the stream id and
    /// the resulting headers (for logging what actually reached the server).
    /// Throws on any protocol violation; from that moment the bytes of the offending block are withheld and
    /// the caller must send a GOAWAY and stop forwarding client bytes.
    /// </summary>
    public sealed class Http2HeaderRewriter(IHpackDecoder clientDecoder, Func<List<H2Header>, bool> rewrite, Action<int, List<H2Header>>? onRewritten = null) : IDisposable
    {
        /// <summary>The minimum SETTINGS_MAX_FRAME_SIZE a peer is guaranteed to accept (RFC 9113 default).</summary>
        private const int MaxOutputFrameSize = 16384;

        private const int MaxHeaderBlock = 256 * 1024;
        private const int MaxInputFrameSize = (1 << 24) - 1; // the peer may have negotiated more than 16 KB

        private readonly H2FrameReader _reader = new(MaxInputFrameSize);
        private readonly IHpackDecoder _clientDecoder = clientDecoder;
        private readonly Func<List<H2Header>, bool> _rewrite = rewrite;
        private readonly Action<int, List<H2Header>>? _onRewritten = onRewritten;

        private readonly MemoryStream _block = new();
        private int _prefaceLeft = H2.Preface.Length;
        private bool _inContinuation;
        private int _blockStream;

        /// <summary>Transforms a chunk of client bytes into the chunk to be sent to the server.</summary>
        public void Transform(ReadOnlySpan<byte> input, ArrayBufferWriter<byte> output)
        {
            if (_prefaceLeft > 0)
            {
                int n = Math.Min(_prefaceLeft, input.Length);
                input[..n].CopyTo(output.GetSpan(n));
                output.Advance(n);
                _prefaceLeft -= n;
                input = input[n..];
            }

            if (input.IsEmpty) return;

            _reader.Append(input);
            while (_reader.TryRead(out var frame))
                EmitFrame(frame, output);
        }

        public void Dispose() => _block.Dispose();

        private void EmitFrame(H2Frame f, ArrayBufferWriter<byte> output)
        {
            if (_inContinuation)
            {
                if (f.Type != H2.Continuation || f.StreamId != _blockStream)
                    throw new InvalidDataException("CONTINUATION expected");
            }
            else if (f.Type == H2.Continuation)
            {
                throw new InvalidDataException("Unexpected CONTINUATION");
            }

            switch (f.Type)
            {
                case H2.Headers:
                    if (f.StreamId == 0) throw new InvalidDataException("HEADERS on stream 0");
                    var p = f.Payload.AsSpan();
                    if ((f.Flags & H2.Padded) != 0)
                    {
                        if (p.IsEmpty) throw new InvalidDataException("Bad padding");
                        int pad = p[0];
                        p = p[1..];
                        if (pad > p.Length) throw new InvalidDataException("Bad padding");
                        p = p[..(p.Length - pad)];
                    }
                    // the PRIORITY field, if present, is dropped: the stream simply gets default priority
                    _block.SetLength(0);
                    _blockStream = f.StreamId;
                    AppendBlock(p);
                    if ((f.Flags & H2.EndHeaders) == 0)
                    {
                        _inContinuation = true;
                        return;
                    }
                    EmitBlock(f.StreamId, (f.Flags & H2.EndStream) != 0, output);
                    return;

                case H2.Continuation:
                    AppendBlock(f.Payload);
                    if ((f.Flags & H2.EndHeaders) == 0) return;
                    _inContinuation = false;
                    EmitBlock(f.StreamId, false, output);
                    return;

                case H2.PushPromise:
                    // a client must not push (RFC 9113, section 8.2)
                    throw new InvalidDataException("PUSH_PROMISE from the client");

                default:
                    // DATA, RST_STREAM, SETTINGS, PING, GOAWAY, WINDOW_UPDATE, PRIORITY, ...
                    CopyFrame(f, output);
                    return;
            }
        }

        private void AppendBlock(ReadOnlySpan<byte> payload)
        {
            if (_block.Length + payload.Length > MaxHeaderBlock)
                throw new InvalidDataException("Header block is too large");
            _block.Write(payload);
        }

        private void EmitBlock(int streamId, bool endStream, ArrayBufferWriter<byte> output)
        {
            var headers = _clientDecoder.Decode(_block.GetBuffer().AsSpan(0, (int)_block.Length));
            _block.SetLength(0);

            if (_rewrite(headers))
            {
                _onRewritten?.Invoke(streamId, headers);
            }

            var encoded = HpackLiteralsEncoder.Encode(headers);
            for (int pos = 0; ; )
            {
                int n = Math.Min(MaxOutputFrameSize, encoded.Length - pos);
                bool last = (pos + n) == encoded.Length;
                byte type = pos == 0 ? H2.Headers : H2.Continuation;
                byte flags = 0;
                if (pos == 0 && endStream) flags |= H2.EndStream;
                if (last) flags |= H2.EndHeaders;
                WriteFrame(output, type, flags, streamId, encoded.AsSpan(pos, n));
                pos += n;
                if (last) break;
            }
        }

        private static void WriteFrame(ArrayBufferWriter<byte> output, byte type, byte flags, int streamId, ReadOnlySpan<byte> payload)
        {
            var span = output.GetSpan(9 + payload.Length);
            span[0] = (byte)(payload.Length >> 16);
            span[1] = (byte)(payload.Length >> 8);
            span[2] = (byte)payload.Length;
            span[3] = type;
            span[4] = flags;
            BinaryPrimitives.WriteInt32BigEndian(span.Slice(5, 4), streamId);
            payload.CopyTo(span.Slice(9));
            output.Advance(9 + payload.Length);
        }

        private static void CopyFrame(H2Frame f, ArrayBufferWriter<byte> output)
            => WriteFrame(output, f.Type, f.Flags, f.StreamId, f.Payload);
    }
}
