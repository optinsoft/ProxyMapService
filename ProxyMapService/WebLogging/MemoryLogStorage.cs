using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading;

namespace ProxyMapService.WebLogging
{
    public class MemoryLogStorage(IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : ILogStorage
    {
        private class LogMessageQueue
        {
            public readonly ConcurrentQueue<LogMessageEntry> Logs = new();
            public int Count;

            public void Clear()
            {
                Logs.Clear();
                Interlocked.Exchange(ref Count, 0);
            }
        }

        private readonly LogMessageQueue[] _queues = [new(), new()];
        private int _currentQueueIndex = 0;

        public void AddLog(LogMessageEntry log)
        {
            var currentSettings = monitoringOptions.CurrentValue;
            if (!currentSettings.EventLog.Enabled) return;

            int index = Volatile.Read(ref _currentQueueIndex);
            var queue = _queues[index];

            queue.Logs.Enqueue(log);
            Interlocked.Increment(ref queue.Count);

            int maxCount = currentSettings.EventLog.MaxEntries;

            while (Volatile.Read(ref queue.Count) > maxCount)
            {
                if (queue.Logs.TryDequeue(out _))
                {
                    Interlocked.Decrement(ref queue.Count);
                }
                else
                {
                    break;
                }
            }
        }

        public IEnumerable<LogMessageEntry> GetRecentLogs()
        {
            int index = Volatile.Read(ref _currentQueueIndex);
            return _queues[index].Logs;
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
