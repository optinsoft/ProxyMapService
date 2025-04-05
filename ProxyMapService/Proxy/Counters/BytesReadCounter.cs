namespace ProxyMapService.Proxy.Counters
{
    public class BytesReadCounter
    {
        private readonly object _lock = new();
        private long _total;

        public long TotalBytesRead { 
            get {
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

        public void OnBytesRead(int bytesRead)
        {
            lock(_lock)
            {
                _total += (long)bytesRead;
            }
            BytesReadEventArgs args = new()
            {
                BytesRead = bytesRead
            };
            BytesReadHandler?.Invoke(this, args);
        }

        public event EventHandler<BytesReadEventArgs>? BytesReadHandler;
    }
}
