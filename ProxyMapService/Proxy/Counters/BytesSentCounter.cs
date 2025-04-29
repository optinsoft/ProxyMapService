using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public class BytesSentCounter
    {
        private readonly object _lock = new();
        private long _total;
        private long _proxified;
        private long _bypassed;

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

        public long ProxyBytesSent
        {
            get
            {
                lock (_lock)
                {
                    return _proxified;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _proxified = value;
                }
            }
        }

        public long BypassBytesSent
        {
            get
            {
                lock (_lock)
                {
                    return _bypassed;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _bypassed = value;
                }
            }
        }

        public void OnBytesSent(SessionContext context, int bytesSent, byte[]? bytesData, int startIndex)
        {
            lock(_lock)
            {
                _total += bytesSent;
                if (context.Proxified)
                {
                    _proxified += (long)bytesSent;
                }
                if (context.Bypassed)
                {
                    _bypassed += (long)bytesSent;
                }
            }
            BytesSentHandler?.Invoke(context, new()
            {
                BytesSent = bytesSent,
                BytesData = bytesData,
                StartIndex = startIndex
            });
        }

        public event EventHandler<BytesSentEventArgs>? BytesSentHandler;
    }
}
