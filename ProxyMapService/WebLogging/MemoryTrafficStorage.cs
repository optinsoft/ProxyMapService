using Microsoft.Extensions.Options;
using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public class MemoryTrafficStorage(IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : IHttpTrafficStorage
    {
        private readonly WebSocketDoubleBufferedQueue _doubleQueue = new();

        public void AddEntry(WebSocketMessageEntry entry)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.TrafficMonitor.Enabled) return;

            int maxCount = currentSettings.TrafficMonitor.MaxEntries;

            _doubleQueue.AddEntry(entry, maxCount);
        }

        public HttpTrafficHistoryDto GetRecentEntries()
        {
            var queue = _doubleQueue.CurrentQueue;

            return new HttpTrafficHistoryDto
            {
                Requests = FilterEntries<HttpRequestMessageEntry, HttpRequestDto>(queue, e => e.Dto),
                Responses = FilterEntries<HttpResponseMessageEntry, HttpResponseDto>(queue, e => e.Dto),
                Completions = FilterEntries<HttpCompletionEntry, HttpCompletionDto>(queue,e => e.Dto),
                RequestBodies = FilterEntries<HttpRequestBodyEntry, HttpBodyDto>(queue,e => e.Dto),
                ResponseBodies = FilterEntries<HttpResponseBodyEntry, HttpBodyDto>(queue,e => e.Dto)
            };
        }

        private static IEnumerable<TResult> FilterEntries<TTarget, TResult>(WebSocketMessageQueue queue, Func<TTarget, TResult> selector)
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
            _doubleQueue.Clear();
        }
    }
}
