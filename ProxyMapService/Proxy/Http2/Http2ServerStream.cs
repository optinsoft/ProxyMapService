namespace ProxyMapService.Proxy.Http2
{
    /// <summary>A single client request on the HTTP/2 server side. The request body is not delivered (discarded).</summary>
    public sealed class Http2ServerStream
    {
        private readonly Http2ServerConnection _connection;
        private readonly CancellationTokenSource _cts;

        internal Http2ServerStream(Http2ServerConnection connection, int streamId, IReadOnlyList<H2Header> headers,
            bool remoteEnded, long window, CancellationToken connectionToken)
        {
            _connection = connection;
            StreamId = streamId;
            Headers = headers;
            RemoteEnded = remoteEnded;
            Window = window;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
        }

        public int StreamId { get; }
        public IReadOnlyList<H2Header> Headers { get; }

        /// <summary>Cancelled if the client sent RST_STREAM or the connection is closed.</summary>
        public CancellationToken Token => _cts.Token;

        internal volatile bool RemoteEnded;
        internal bool ResponseStarted;
        internal bool ResponseEnded;
        internal long Window; // under the connection lock

        internal void Cancel()
        {
            try { _cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        internal void DisposeToken() => _cts.Dispose();

        /// <summary>Response headers; names are lowercased.</summary>
        public Task SendHeadersAsync(int status, IReadOnlyList<(string Name, string Value)> headers, bool endStream)
            => _connection.SendHeadersAsync(this, status, headers, endStream);

        /// <summary>Response body subject to the client's flow control. The last call is made with endStream = true.</summary>
        public Task SendDataAsync(ReadOnlyMemory<byte> data, bool endStream)
            => _connection.SendDataAsync(this, data, endStream);
    }
}
