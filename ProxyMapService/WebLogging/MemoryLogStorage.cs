using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ProxyMapService.WebLogging
{
    public class MemoryLogStorage(IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : ILogStorage
    {
        private readonly WebSocketDoubleBufferedQueue _doubleQueue = new();

        public void AddLog(LogMessageEntry log)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.EventLog.Enabled) return;

            int maxCount = currentSettings.EventLog.MaxEntries;

            _doubleQueue.AddEntry(log, maxCount);
        }

        public IEnumerable<LogMessageEntry> GetRecentLogs()
        {
            var queue = _doubleQueue.CurrentQueue;

            foreach (var entry in queue.Entries)
            {
                if (entry is LogMessageEntry logEntry)
                {
                    yield return logEntry;
                }
            }
        }

        public void Clear()
        {
            _doubleQueue.Clear();
        }
    }
}
