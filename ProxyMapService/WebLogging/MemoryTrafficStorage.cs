using Microsoft.Extensions.Options;
using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public class MemoryTrafficStorage(IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : IHttpTrafficStorage
    {
        private readonly WebSocketDoubleBufferedQueue _requestQueue = new();
        private readonly WebSocketDoubleBufferedQueue _responseQueue = new();
        private readonly WebSocketDoubleBufferedQueue _completionQueue = new();
        private readonly WebSocketDoubleBufferedQueue _requestBodyQueue = new();
        private readonly WebSocketDoubleBufferedQueue _responseBodyQueue = new();

        public void AddRequestEntry(HttpRequestMessageEntry entry)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.TrafficMonitor.Enabled) return;

            int maxCount = currentSettings.TrafficMonitor.MaxEntries;

            _requestQueue.AddEntry(entry, maxCount);
        }

        public void AddResponseEntry(HttpResponseMessageEntry entry)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.TrafficMonitor.Enabled) return;

            int maxCount = currentSettings.TrafficMonitor.MaxEntries;

            _responseQueue.AddEntry(entry, maxCount);
        }

        public void AddCompletionEntry(HttpCompletionEntry entry)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.TrafficMonitor.Enabled) return;

            int maxCount = currentSettings.TrafficMonitor.MaxEntries;

            _completionQueue.AddEntry(entry, maxCount);
        }

        public void AddRequestBodyEntry(HttpRequestBodyEntry entry)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.TrafficMonitor.Enabled) return;

            int maxCount = currentSettings.TrafficMonitor.MaxEntries;

            _requestBodyQueue.AddEntry(entry, maxCount);
        }

        public void AddResponseBodyEntry(HttpResponseBodyEntry entry)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.TrafficMonitor.Enabled) return;

            int maxCount = currentSettings.TrafficMonitor.MaxEntries;

            _responseBodyQueue.AddEntry(entry, maxCount);
        }

        public HttpTrafficHistoryDto GetRecentEntries()
        {
            return new HttpTrafficHistoryDto
            {
                Requests = FilterEntries<HttpRequestMessageEntry, HttpRequestDto>(_requestQueue.CurrentQueue, e => e.Dto),
                Responses = FilterEntries<HttpResponseMessageEntry, HttpResponseDto>(_responseQueue.CurrentQueue, e => e.Dto),
                Completions = FilterEntries<HttpCompletionEntry, HttpCompletionDto>(_completionQueue.CurrentQueue, e => e.Dto),
                RequestBodies = FilterEntries<HttpRequestBodyEntry, HttpBodyDto>(_requestBodyQueue.CurrentQueue, e => e.Dto),
                ResponseBodies = FilterEntries<HttpResponseBodyEntry, HttpBodyDto>(_responseBodyQueue.CurrentQueue, e => e.Dto)
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
            _requestQueue.Clear();
            _responseQueue.Clear();
            _completionQueue.Clear();
            _requestBodyQueue.Clear();
            _responseBodyQueue.Clear();
        }
    }
}
