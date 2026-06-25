using Microsoft.Extensions.Options;
using ProxyMapService.WebLogging.Dtos;
using System.Collections.Concurrent;

namespace ProxyMapService.WebLogging
{
    public class MemoryTrafficStorage(IOptions<WebSocketMonitoringOptions> monitoringOptions) : IHttpTrafficStorage
    {
        private readonly ConcurrentQueue<WebSocketMessageEntry> _entries = new();
        private readonly WebSocketMonitoringOptions _settings = monitoringOptions.Value;

        public void AddEntry(WebSocketMessageEntry entry)
        {
            if (!_settings.TrafficMonitor.Enabled) return;

            _entries.Enqueue(entry);

            int maxCount = _settings.TrafficMonitor.MaxEntries;

            while (_entries.Count > maxCount) 
            {
                _entries.TryDequeue(out _);
            }
        }

        public HttpTrafficHistoryResponse GetRecentEntries()
        {
            var snapshot = _entries.ToArray();

            return new HttpTrafficHistoryResponse
            {
                Requests = snapshot
                    .OfType<HttpRequestMessageEntry>()
                    .Select(entry => entry.Dto)
                    .ToArray(),
                Responses = snapshot
                    .OfType<HttpResponseMessageEntry>()
                    .Select(entry => entry.Dto)
                    .ToArray(),
                RequestBodies = snapshot
                    .OfType<HttpRequestBodyEntry>()
                    .Select(entry => entry.Dto)
                    .ToArray(),
                ResponseBodies = snapshot
                    .OfType<HttpResponseBodyEntry>()
                    .Select (entry => entry.Dto)
                    .ToArray(),
            };
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
