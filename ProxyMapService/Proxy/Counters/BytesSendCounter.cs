using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public class BytesSendCounter(StreamDirection direction) : IBytesSendCounter
    {
        private bool _logSending;
        private readonly StreamDirection _direction = direction;
        private readonly object _lock = new();
        private long _totalBytes;
        private long _proxifiedBytes;
        private long _bypassedBytes;
        private long _cachedBytes;

        public bool LogSending { get => _logSending; set => _logSending = value; }

        public StreamDirection Direction { get { return _direction; } }

        public long TotalBytesSent
        {
            get
            {
                lock (_lock)
                {
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

        public long ProxyBytesSent
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

        public long BypassBytesSent
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

        public long CacheBytesSent
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
                lock ( _lock)
                {
                    _cachedBytes = value;
                }
            }
        }

        public bool IsLogSending { get => _logSending; }

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

        public void OnBytesSend(SessionContext context, int bytesSend, byte[]? bytesData, int startIndex, long tunnelId)
        {
            BytesSendHandler?.Invoke(context, new()
            {
                BytesSend = bytesSend,
                BytesData = bytesData,
                StartIndex = startIndex,
                Direction = Direction,
                TunnelId = tunnelId
            });
        }

        public void OnBytesSent(SessionContext context, int bytesSent, byte[]? bytesData, int startIndex, long tunnelId)
        {
            lock (_lock)
            {
                _totalBytes += bytesSent;
                if (context.Proxified)
                {
                    _proxifiedBytes += (long)bytesSent;
                }
                if (context.Bypassed)
                {
                    _bypassedBytes += (long)bytesSent;
                }
                if (context.CachedReply)
                {
                    _cachedBytes += (long)bytesSent;
                }
            }
            BytesSentHandler?.Invoke(context, new()
            {
                BytesSent = bytesSent,
                BytesData = bytesData,
                StartIndex = startIndex,
                Direction = Direction,
                TunnelId = tunnelId
            });
        }

        public event EventHandler<BytesSendEventArgs>? BytesSendHandler;
        public event EventHandler<BytesSentEventArgs>? BytesSentHandler;
    }
}
