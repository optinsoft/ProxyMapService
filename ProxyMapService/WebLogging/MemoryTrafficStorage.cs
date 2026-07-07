using Microsoft.Extensions.Options;
using ProxyMapService.WebLogging.Dtos;
using System.Collections.Concurrent;

namespace ProxyMapService.WebLogging
{
    public class MemoryTrafficStorage(IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : IHttpTrafficStorage
    {
        private readonly ConcurrentQueue<WebSocketMessageEntry> _entries = new();

        public void AddEntry(WebSocketMessageEntry entry)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.TrafficMonitor.Enabled) return;

            _entries.Enqueue(entry);

            int maxCount = currentSettings.TrafficMonitor.MaxEntries;

            while (_entries.Count > maxCount && _entries.TryDequeue(out _))
            {
            }
        }

        public HttpTrafficHistoryDto GetRecentEntries()
        {
            return new HttpTrafficHistoryDto
            {
                Requests = FilterEntries<HttpRequestMessageEntry, HttpRequestDto>(e => e.Dto),
                Responses = FilterEntries<HttpResponseMessageEntry, HttpResponseDto>(e => e.Dto),
                Completions = FilterEntries<HttpCompletionEntry, HttpCompletionDto>(e => e.Dto),
                RequestBodies = FilterEntries<HttpRequestBodyEntry, HttpBodyDto>(e => e.Dto),
                ResponseBodies = FilterEntries<HttpResponseBodyEntry, HttpBodyDto>(e => e.Dto)
            };
        }

        private IEnumerable<TResult> FilterEntries<TTarget, TResult>(Func<TTarget, TResult> selector)
            where TTarget : WebSocketMessageEntry
        {
            foreach (var entry in _entries)
            {
                if (entry is TTarget target)
                {
                    yield return selector(target);
                }
            }
        }

        public void Clear()
        {
            _entries.Clear();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }
}
