using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ProxyMapService.WebLogging
{
    public class MemoryLogStorage(IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : ILogStorage
    {
        private readonly ConcurrentQueue<LogMessageEntry> _logs = new();
        private readonly object _cleanupLock = new();

        public void AddLog(LogMessageEntry log)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.EventLog.Enabled) return;

            _logs.Enqueue(log);
            
            int maxCount = currentSettings.EventLog.MaxEntries;

            if (_logs.Count > maxCount)
            {
                lock (_cleanupLock)
                {
                    while (_logs.Count > maxCount)
                    {
                        _logs.TryDequeue(out _);
                    }
                }
            }
        }

        public IEnumerable<LogMessageEntry> GetRecentLogs()
        {
            return _logs.ToArray();
        }

        public void Clear()
        {
            _logs.Clear();
        }
    }
}
