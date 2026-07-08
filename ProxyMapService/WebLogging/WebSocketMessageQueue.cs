using System.Collections.Concurrent;

namespace ProxyMapService.WebLogging
{
    public class WebSocketMessageQueue
    {
        public readonly ConcurrentQueue<WebSocketMessageEntry> Entries = new();
        private int _count = 0;

        public void AddEntry(WebSocketMessageEntry entry, int maxCount)
        {
            Entries.Enqueue(entry);
            Interlocked.Increment(ref _count);

            while (Volatile.Read(ref _count) > maxCount)
            {
                if (Entries.TryDequeue(out _))
                {
                    Interlocked.Decrement(ref _count);
                }
                else
                {
                    break;
                }
            }
        }

        public void Clear()
        {
            Entries.Clear();
            Interlocked.Exchange(ref _count, 0);
        }
    }
}
