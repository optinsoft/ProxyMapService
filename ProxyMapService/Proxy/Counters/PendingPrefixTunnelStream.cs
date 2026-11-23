namespace ProxyMapService.Proxy.Counters
{
    /// <summary>
    /// ITunnelStream wrapper that yields the stashed (rewritten) HTTP request bytes
    /// (SessionContext.PendingOutgoingBytes) before the inner stream's data. Used by the
    /// H1->H2 translation tunnel when the first client request was withheld until the
    /// upstream TLS handshake completed: the request must be re-encoded into HTTP/2 frames
    /// together with the following requests, not written raw into the h2 connection.
    /// </summary>
    public class PendingPrefixTunnelStream : ITunnelStream
    {
        private readonly ITunnelStream _inner;
        private ReadOnlyMemory<byte> _pending;

        public PendingPrefixTunnelStream(ITunnelStream inner, byte[] pending)
        {
            _inner = inner;
            _pending = pending;
        }

        public event EventHandler? DisconnectHandler
        {
            add => _inner.DisconnectHandler += value;
            remove => _inner.DisconnectHandler -= value;
        }

        public void OnDisconnected() => _inner.OnDisconnected();

        public void TransferHandlersTo(ITunnelStream target) => _inner.TransferHandlersTo(target);

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_pending.IsEmpty)
            {
                var count = Math.Min(_pending.Length, buffer.Length);
                _pending[..count].CopyTo(buffer);
                _pending = _pending[count..];
                return count;
            }
            return await _inner.ReadAsync(buffer, cancellationToken);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.WriteAsync(buffer, cancellationToken);
    }
}
