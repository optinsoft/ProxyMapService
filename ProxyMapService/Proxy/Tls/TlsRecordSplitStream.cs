using System.Buffers.Binary;
using ProxyMapService.Proxy.Counters;

namespace ProxyMapService.Proxy.Tls
{
    /// <summary>
    /// A stream wrapper inserted between the outgoing TLS implementation
    /// (SslStream / BoringSslStream / NssStream) and the raw outgoing stream.
    /// It intercepts the very first TLS record written by the TLS implementation and,
    /// when <see cref="TlsRecordSplitSpec"/> is configured, re-chunks that record
    /// (the ClientHello) into several separate TLS records. All subsequent writes and
    /// reads pass through unchanged.
    /// </summary>
    /// <remarks>
    /// It is also used in passthrough mode (no MITM): there the client's own ClientHello
    /// travels through the tunnel unchanged, so the splitter is applied directly on the
    /// outgoing stream and acts as the tunnel destination stream. In both modes byte
    /// counting and disconnect handling are delegated to the wrapped stream, which is
    /// a <see cref="CountingStream"/> — the splitter only forwards the
    /// <see cref="ITunnelStream"/> members to it.
    /// </remarks>
    /// <remarks>
    /// The wrapper needs to see whole records, so an incomplete first record is buffered
    /// until enough bytes have been written by the TLS implementation. The maximum TLS
    /// record size is 16 KB + headers, so the buffer never grows beyond that in practice.
    /// </remarks>
    public sealed class TlsRecordSplitStream : Stream, ITunnelStream
    {
        private const int MaxRecordSize = 16384 + 5;
        private const int MaxBuffered = 64 * 1024;

        private readonly Stream _inner;
        private readonly TlsRecordSplitSpec _spec;
        private readonly byte[] _buffer = new byte[MaxBuffered];
        private int _buffered;
        private bool _firstRecordDone;

        public TlsRecordSplitStream(Stream inner, TlsRecordSplitSpec spec)
        {
            _inner = inner;
            _spec = spec;
        }

        /// <summary>The wrapped stream is the counting owner; forward everything to it.</summary>
        private ITunnelStream? TunnelInner => _inner as ITunnelStream;

        public void OnDisconnected() => TunnelInner?.OnDisconnected();

        public void TransferHandlersTo(ITunnelStream target) => TunnelInner?.TransferHandlersTo(target);

        public event EventHandler? DisconnectHandler
        {
            add { var ts = TunnelInner; if (ts != null) ts.DisconnectHandler += value; }
            remove { var ts = TunnelInner; if (ts != null) ts.DisconnectHandler -= value; }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_firstRecordDone)
            {
                await _inner.WriteAsync(buffer, cancellationToken);
                return;
            }

            if (buffer.Length > 0)
            {
                buffer.Span.CopyTo(_buffer.AsSpan(_buffered));
                _buffered += buffer.Length;
            }

            if (_buffered < 5)
            {
                return;
            }

            byte contentType = _buffer[0];
            // Sanity check: the first record must look like a TLS record at all
            // (0x16 = handshake). Otherwise just pass the data through untouched.
            if (contentType > 0x17)
            {
                await FlushBufferedAsync(cancellationToken);
                return;
            }

            int recordLen = 5 + BinaryPrimitives.ReadUInt16BigEndian(_buffer.AsSpan(3));

            if (recordLen > _buffered)
            {
                if (recordLen > MaxBuffered)
                {
                    // Cannot possibly be a sane first record — pass everything through.
                    await FlushBufferedAsync(cancellationToken);
                    return;
                }
                return; // wait for the rest of the first record
            }

            _firstRecordDone = true;

            // 0x16 + handshake type 0x01 = ClientHello record, the one that carries the SNI.
            if (contentType == 0x16 && recordLen > 5 && _buffer[5] == 0x01)
            {
                int? sniStart = null, sniEnd = null;
                if (TlsClientHelloParser.TryGetSniOffsets(_buffer.AsSpan(0, recordLen), out var s, out var e))
                {
                    sniStart = s;
                    sniEnd = e;
                }
                var segments = _spec.BuildSplitRecordSegments(_buffer.AsSpan(0, recordLen), sniStart, sniEnd);
                if (segments != null)
                {
                    // Each resulting TLS record is written separately
                    foreach (var segment in segments)
                    {
                        await _inner.WriteAsync(segment, cancellationToken);
                    }
                }
                else
                {
                    await _inner.WriteAsync(_buffer.AsMemory(0, recordLen), cancellationToken);
                }
            }
            else
            {
                await _inner.WriteAsync(_buffer.AsMemory(0, recordLen), cancellationToken);
            }

            if (_buffered > recordLen)
            {
                await _inner.WriteAsync(_buffer.AsMemory(recordLen, _buffered - recordLen), cancellationToken);
            }
        }

        private async ValueTask FlushBufferedAsync(CancellationToken cancellationToken)
        {
            _firstRecordDone = true;
            if (_buffered > 0)
            {
                await _inner.WriteAsync(_buffer.AsMemory(0, _buffered), cancellationToken);
                _buffered = 0;
            }
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
