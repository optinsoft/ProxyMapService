using Microsoft.Extensions.Options;
using ProxyMapService.WebLogging.Dtos;
using System.Collections.Concurrent;

namespace ProxyMapService.WebLogging
{
    public class MemoryTrafficStorage(IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : IHttpTrafficStorage
    {
        private readonly ConcurrentQueue<WebSocketMessageEntry> _entries = new();
        private readonly object _cleanupLock = new();

        public void AddEntry(WebSocketMessageEntry entry)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.TrafficMonitor.Enabled) return;

            _entries.Enqueue(entry);

            int maxCount = currentSettings.TrafficMonitor.MaxEntries;

            if (_entries.Count > maxCount)
            {
                lock (_cleanupLock)
                {
                    while (_entries.Count > maxCount)
                    {
                        _entries.TryDequeue(out _);
                    }
                }
            }
        }

        public HttpTrafficHistoryDto GetRecentEntries()
        {
            var snapshot = _entries.ToArray();

            return new HttpTrafficHistoryDto
            {
                Requests = snapshot
                    .OfType<HttpRequestMessageEntry>()
                    .Select(entry => entry.Dto)
                    .ToArray(),
                Responses = snapshot
                    .OfType<HttpResponseMessageEntry>()
                    .Select(entry => entry.Dto)
                    .ToArray(),
                Completions = snapshot
                    .OfType<HttpCompletionEntry>()
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
