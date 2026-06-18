using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ProxyMapService.WebLogging
{
    public class MemoryLogStorage(IOptions<WebSocketMonitoringOptions> monitoringOptions) : ILogStorage
    {
        private readonly ConcurrentQueue<LogMessageEntry> _logs = new();
        private readonly WebSocketMonitoringOptions _settings = monitoringOptions.Value;

        public void AddLog(LogMessageEntry log)
        {
            if (!_settings.EventLog.Enabled) return;

            _logs.Enqueue(log);
            
            int maxCount = _settings.EventLog.MaxEntries;

            while (_logs.Count > maxCount)
            {
                _logs.TryDequeue(out _);
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
