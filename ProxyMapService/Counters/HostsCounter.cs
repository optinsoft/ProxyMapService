using ProxyMapService.Proxy.Network;
using ProxyMapService.Models;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Counters
{
    public class HostsCounter
    {
        private readonly Dictionary<string, HostStats> _hostStats = new();
        private readonly object _lock = new();

        public void Reset()
        {
            lock (_lock)
            {
                _hostStats.Clear();
            }
        }

        public Dictionary<string, HostStats> GetHostStats()
        {
            Dictionary<string, HostStats> stats;
            lock (_lock)
            {
                stats = _hostStats.ToDictionary(entry => entry.Key, entry => entry.Value);
            }
            return stats;
        }

        public void OnHostConnected(object? sender, EventArgs e)
        {
            if (sender == null) return;
            var context = (SessionContext)sender;
            lock (_lock)
            {
                var hostStats = _hostStats.GetValueOrDefault(context.HostName, new()
                {
                    Count = 0,
                    Proxified = context.Proxified,
                    Bypassed = context.Bypassed,
                    BytesRead = 0,
                    BytesSent = 0
                });
                hostStats.Count += 1;
                _hostStats[context.HostName] = hostStats;
            }
        }

        public void OnBytesRead(object? sender, BytesReadEventArgs e)
        {
            if (sender == null) return;
            var context = (SessionContext)sender;
            lock (_lock)
            {
                if (_hostStats.TryGetValue(context.HostName, out HostStats? hostStats))
                {
                    hostStats.BytesRead += e.BytesRead;
                    _hostStats[context.HostName] = hostStats;
                }
            }
        }

        public void OnBytesSent(object? sender, BytesSentEventArgs e)
        {
            if (sender == null) return;
            var context = (SessionContext)sender;
            lock (_lock)
            {
                if (_hostStats.TryGetValue(context.HostName, out HostStats? hostStats))
                {
                    hostStats.BytesSent += e.BytesSent;
                    _hostStats[context.HostName] = hostStats;
                }
            }
        }
    }
}
