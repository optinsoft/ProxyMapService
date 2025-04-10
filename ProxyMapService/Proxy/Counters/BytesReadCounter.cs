using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public class BytesReadCounter
    {
        private readonly object _lock = new();
        private long _total;
        private long _proxified;
        private long _bypassed;

        public long TotalBytesRead { 
            get 
            {
                lock (_lock) {
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

        public long ProxyBytesRead
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

        public long BypassBytesRead
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

        public void OnBytesRead(SessionContext context, int bytesRead)
        {
            lock(_lock)
            {
                _total += (long)bytesRead;
                if (context.Proxified)
                {
                    _proxified += (long)bytesRead;
                }
                if (context.Bypassed)
                {
                    _bypassed += (long)bytesRead;
                }
            }
            BytesReadHandler?.Invoke(context, new()
            {
                BytesRead = bytesRead
            });
        }

        public event EventHandler<BytesReadEventArgs>? BytesReadHandler;
    }
}
