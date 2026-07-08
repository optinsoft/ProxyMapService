namespace ProxyMapService.WebLogging
{
    public class WebSocketDoubleBufferedQueue
    {
        private readonly WebSocketMessageQueue[] _queues = [new(), new()];
        private int _currentQueueIndex = 0;

        public WebSocketMessageQueue CurrentQueue
        {
            get
            {
                int index = Volatile.Read(ref _currentQueueIndex);
                return _queues[index];
            }
        }

        public void AddEntry(WebSocketMessageEntry entry, int maxCount)
        {
            int currentIndex = Volatile.Read(ref _currentQueueIndex);
            _queues[currentIndex].AddEntry(entry, maxCount);
        }

        public void Clear()
        {
            int currentIndex = Volatile.Read(ref _currentQueueIndex);
            int nextIndex = 1 - currentIndex;

            var nextQueue = _queues[nextIndex];

            nextQueue.Clear();

            Interlocked.Exchange(ref _currentQueueIndex, nextIndex);

            Thread.SpinWait(100);

            _queues[currentIndex].Clear();
        }
    }
}
