using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public class BytesSentCounter
    {
        private readonly object _lock = new();
        private long _total;

        public long TotalBytesSent
        {
            get
            {
                lock (_lock)
                {
                    return _total;
                }

            }
            private set
            {
                lock (_lock)
                {
                    _total = value;
                }
            }
        }

        public void OnBytesSent(SessionContext context, int bytesSent)
        {
            lock(_lock)
            {
                _total += bytesSent;
            }
            BytesSentEventArgs args = new()
            {
                BytesSent = bytesSent
            };
            BytesSentHandler?.Invoke(context, args);
        }

        public event EventHandler<BytesSentEventArgs>? BytesSentHandler;
    }
}
