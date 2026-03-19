using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public class BytesReadCounter(StreamDirection direction) : IBytesReadCounter
    {
        private bool _logReading;
        private readonly StreamDirection _direction = direction;
        private readonly object _lock = new();
        private long _totalBytes;
        private long _proxifiedBytes;
        private long _bypassedBytes;
        private long _cachedBytes;
        private bool _cached;

        public bool LogReading { get => _logReading; set => _logReading = value; }

        public StreamDirection Direction => _direction;

        public long TotalBytesRead { 
            get 
            {
                lock (_lock) {
                    return _totalBytes;
                }

            }
            private set
            {
                lock (_lock)
                {
                    _totalBytes = value;
                }
            }
        }

        public long ProxyBytesRead
        {
            get 
            {
                lock (_lock)
                {
                    return _proxifiedBytes;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _proxifiedBytes = value;
                }
            }
        }

        public long BypassBytesRead
        {
            get
            {
                lock (_lock)
                {
                    return _bypassedBytes;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _bypassedBytes = value;
                }
            }
        }

        public long CachedBytesRead
        {
            get 
            {
                lock (_lock)
                {
                    return _cachedBytes;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _cachedBytes = value;
                }
            }
        }

        public bool IsLogReading => _logReading;

        public bool Cached {
            get => _cached; 
            set
            {
                _cached = value;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _totalBytes = 0;
                _proxifiedBytes = 0;
                _bypassedBytes = 0;
                _cachedBytes = 0;
            }
        }

        public void OnBytesRead(SessionContext context, int bytesRead, byte[]? bytesData, int startIndex, long tunnelId)
        {
            lock (_lock)
            {
                _totalBytes += (long)bytesRead;
                if (context.Proxified)
                {
                    _proxifiedBytes += (long)bytesRead;
                }
                if (context.Bypassed)
                {
                    _bypassedBytes += (long)bytesRead;
                }
                if (_cached)
                {
                    _cachedBytes += (long)bytesRead;
                }
            }
            BytesReadHandler?.Invoke(context, new()
            {
                BytesRead = bytesRead,
                BytesData = bytesData,
                StartIndex = startIndex,
                Direction = Direction,
                TunnelId = tunnelId
            });
        }

        public event EventHandler<BytesReadEventArgs>? BytesReadHandler;
    }
}
