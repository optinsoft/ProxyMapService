namespace ProxyMapService.Proxy.Counters
{
    /// <summary>
    /// The stream contract required by the tunnel implementations. A C# interface cannot
    /// inherit the abstract <see cref="Stream"/> class, so it declares the I/O members the
    /// tunnels actually use; every implementation is still a Stream, and the member
    /// signatures intentionally match the Stream ones so that a single public method
    /// implements both interfaces. Besides CountingStream, wrapper streams (e.g.
    /// TlsRecordSplitStream) implement this contract, so tunnels do not depend on the
    /// concrete stream type.
    /// </summary>
    public interface ITunnelStream
    {
        /// <summary>Tunnel read.</summary>
        ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

        /// <summary>Tunnel write.</summary>
        ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);

        /// <summary>Raised when the stream reached EOF (the remote side closed the connection).</summary>
        event EventHandler? DisconnectHandler;

        /// <summary>Invoked by the tunnel when a read fails with an IOException (EOF).</summary>
        void OnDisconnected();

        /// <summary>
        /// Moves the disconnect handlers subscribed to this stream to the target stream,
        /// so that after a stream hand-off the disconnect is reported by the new owner.
        /// </summary>
        void TransferHandlersTo(ITunnelStream target);
    }
}
