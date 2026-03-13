namespace ProxyMapService.Proxy.Counters
{
    public class ProxyCounters
    {
        private readonly SessionsCounter _sessionsCounter = new();
        private readonly BytesReadCounter _outgoingReadCounter = new(StreamDirection.Upstream);
        private readonly BytesSentCounter _outgoingSentCounter = new(StreamDirection.Upstream);
        private readonly BytesReadCounter _incomingReadCounter = new(StreamDirection.Downstream);
        private readonly BytesSentCounter _incomingSentCounter = new(StreamDirection.Downstream);
        private readonly BytesReadCounter _incomingReadSslCounter = new(StreamDirection.Downstream);
        private readonly BytesReadCounter _outgoingReadSslCounter = new(StreamDirection.Upstream);
        private readonly BytesSentCounter _incomingSentSslCounter = new(StreamDirection.Downstream);
        private readonly BytesSentCounter _outgoingSentSslCounter = new(StreamDirection.Upstream);
        private readonly HttpHeadersLogger _httpRequestHeadersLogger = new(false);
        private readonly HttpHeadersLogger _httpResponseHeadersLogger = new(true);

        public SessionsCounter SessionsCounter { get => _sessionsCounter; }
        public BytesReadCounter OutgoingReadCounter { get => _outgoingReadCounter; }
        public BytesSentCounter OutgoingSentCounter { get => _outgoingSentCounter; }
        public BytesReadCounter IncomingReadCounter { get => _incomingReadCounter; }
        public BytesSentCounter IncomingSentCounter {  get => _incomingSentCounter; }
        public BytesReadCounter IncomingReadSslCounter { get => _incomingReadSslCounter; }
        public BytesReadCounter OutgoingReadSslCounter { get => _outgoingReadSslCounter; }
        public BytesSentCounter IncomingSentSslCounter { get => _incomingSentSslCounter; }
        public BytesSentCounter OutgoingSentSslCounter { get => _outgoingSentSslCounter; }
        public HttpHeadersLogger HttpRequestHeadersLogger { get => _httpRequestHeadersLogger; }
        public HttpHeadersLogger HttpResponseHeadersLogger { get => _httpResponseHeadersLogger; }

        public void ResetStats()
        {
            _sessionsCounter.Reset();
            _outgoingReadCounter.Reset();
            _outgoingSentCounter.Reset();
            _incomingReadCounter.Reset();
            _incomingSentCounter.Reset();
            _incomingReadSslCounter.Reset();
            _outgoingReadSslCounter.Reset();
            _incomingSentSslCounter.Reset();
            _outgoingSentSslCounter.Reset();
        }
    }
}
