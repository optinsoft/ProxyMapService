using Microsoft.Extensions.Options;
using ProxyMapService.WebLogging.Dtos;
using System.Collections.Concurrent;

namespace ProxyMapService.WebLogging
{
    public class MemoryTrafficStorage(IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : IHttpTrafficStorage
    {
        private class TrafficQueue
        {
            public readonly ConcurrentQueue<WebSocketMessageEntry> Entries = new();
            public int Count;

            public void Clear()
            {
                Entries.Clear();
                Interlocked.Exchange(ref Count, 0);
            }
        }

        private readonly TrafficQueue[] _queues = [new(), new()];
        private int _currentQueueIndex = 0;

        public void AddEntry(WebSocketMessageEntry entry)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.TrafficMonitor.Enabled) return;

            int index = Volatile.Read(ref _currentQueueIndex);
            var queue = _queues[index];

            queue.Entries.Enqueue(entry);
            Interlocked.Increment(ref queue.Count);

            int maxCount = currentSettings.TrafficMonitor.MaxEntries;

            while (Volatile.Read(ref queue.Count) > maxCount)
            {
                if (queue.Entries.TryDequeue(out _))
                {
                    Interlocked.Decrement(ref queue.Count);
                }
                else
                {
                    break;
                }
            }
        }

        public HttpTrafficHistoryDto GetRecentEntries()
        {
            int index = Volatile.Read(ref _currentQueueIndex);
            var queue = _queues[index];

            return new HttpTrafficHistoryDto
            {
                Requests = FilterEntries<HttpRequestMessageEntry, HttpRequestDto>(queue, e => e.Dto),
                Responses = FilterEntries<HttpResponseMessageEntry, HttpResponseDto>(queue, e => e.Dto),
                Completions = FilterEntries<HttpCompletionEntry, HttpCompletionDto>(queue,e => e.Dto),
                RequestBodies = FilterEntries<HttpRequestBodyEntry, HttpBodyDto>(queue,e => e.Dto),
                ResponseBodies = FilterEntries<HttpResponseBodyEntry, HttpBodyDto>(queue,e => e.Dto)
            };
        }

        private IEnumerable<TResult> FilterEntries<TTarget, TResult>(TrafficQueue queue, Func<TTarget, TResult> selector)
            where TTarget : WebSocketMessageEntry
        {
            foreach (var entry in queue.Entries)
            {
                if (entry is TTarget target)
                {
                    yield return selector(target);
                }
            }
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
