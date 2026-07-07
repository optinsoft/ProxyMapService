using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ProxyMapService.WebLogging
{
    public class MemoryLogStorage(IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : ILogStorage
    {
        private readonly ConcurrentQueue<LogMessageEntry> _logs = new();
        
        public void AddLog(LogMessageEntry log)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.EventLog.Enabled) return;

            _logs.Enqueue(log);
            
            int maxCount = currentSettings.EventLog.MaxEntries;

            while (_logs.Count > maxCount && _logs.TryDequeue(out _))
            {
            }
        }

        public IEnumerable<LogMessageEntry> GetRecentLogs()
        {
            foreach (var log in _logs)
            {
                yield return log;
            }
        }

        public void Clear()
        {
            _logs.Clear();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }
}
