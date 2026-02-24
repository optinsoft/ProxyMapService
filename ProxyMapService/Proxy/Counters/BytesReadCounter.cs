using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public class BytesReadCounter(StreamDirection direction) : IBytesReadCounter
    {
        private bool _logReading;
        private readonly StreamDirection _direction = direction;
        private readonly object _lock = new();
        private long _total;
        private long _proxified;
        private long _bypassed;

        public bool LogReading { get => _logReading; set => _logReading = value; }

        public StreamDirection Direction => _direction;

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

        public bool IsLogReading => _logReading;

        public void Reset()
        {
            lock (_lock)
            {
                _total = 0;
                _proxified = 0;
                _bypassed = 0;
            }
        }

        public void OnBytesRead(SessionContext context, int bytesRead, byte[]? bytesData, int startIndex)
        {
            lock (_lock)
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
                BytesRead = bytesRead,
                BytesData = bytesData,
                StartIndex = startIndex,
                Direction = Direction
            });
        }

        public event EventHandler<BytesReadEventArgs>? BytesReadHandler;
    }
}
