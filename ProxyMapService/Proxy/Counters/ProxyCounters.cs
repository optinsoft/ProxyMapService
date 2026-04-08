namespace ProxyMapService.Proxy.Counters
{
    public class ProxyCounters
    {
        private readonly SessionsCounter _sessionsCounter = new();
        private readonly BytesReadCounter _outgoingReadCounter = new(StreamDirection.Upstream);
        private readonly BytesSendCounter _outgoingSendCounter = new(StreamDirection.Upstream);
        private readonly BytesReadCounter _incomingReadCounter = new(StreamDirection.Downstream);
        private readonly BytesSendCounter _incomingSendCounter = new(StreamDirection.Downstream);
        private readonly BytesReadCounter _incomingReadSslCounter = new(StreamDirection.Downstream);
        private readonly BytesReadCounter _outgoingReadSslCounter = new(StreamDirection.Upstream);
        private readonly BytesSendCounter _incomingSendSslCounter = new(StreamDirection.Downstream);
        private readonly BytesSendCounter _outgoingSendSslCounter = new(StreamDirection.Upstream);
        private readonly HttpHeadersLogger _httpRequestHeadersLogger = new(false);
        private readonly HttpHeadersLogger _httpResponseHeadersLogger = new(true);

        public SessionsCounter SessionsCounter { get => _sessionsCounter; }
        public BytesReadCounter OutgoingReadCounter { get => _outgoingReadCounter; }
        public BytesSendCounter OutgoingSendCounter { get => _outgoingSendCounter; }
        public BytesReadCounter IncomingReadCounter { get => _incomingReadCounter; }
        public BytesSendCounter IncomingSendCounter {  get => _incomingSendCounter; }
        public BytesReadCounter IncomingReadSslCounter { get => _incomingReadSslCounter; }
        public BytesReadCounter OutgoingReadSslCounter { get => _outgoingReadSslCounter; }
        public BytesSendCounter IncomingSendSslCounter { get => _incomingSendSslCounter; }
        public BytesSendCounter OutgoingSendSslCounter { get => _outgoingSendSslCounter; }
        public HttpHeadersLogger HttpRequestHeadersLogger { get => _httpRequestHeadersLogger; }
        public HttpHeadersLogger HttpResponseHeadersLogger { get => _httpResponseHeadersLogger; }

        public void ResetStats()
        {
            _sessionsCounter.Reset();
            _outgoingReadCounter.Reset();
            _outgoingSendCounter.Reset();
            _incomingReadCounter.Reset();
            _incomingSendCounter.Reset();
            _incomingReadSslCounter.Reset();
            _outgoingReadSslCounter.Reset();
            _incomingSendSslCounter.Reset();
            _outgoingSendSslCounter.Reset();
        }
    }
}
