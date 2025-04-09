using Proxy.Network;
using ProxyMapService.Models;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Counters
{
    public class HostsCounter
    {
        private readonly Dictionary<string, HostStats> _hostStats = new();
        private readonly object _lock = new();

        public Dictionary<string, HostStats> GetHostStats()
        {
            Dictionary<string, HostStats> stats;
            lock (_lock)
            {
                stats = _hostStats.ToDictionary(entry => entry.Key, entry => entry.Value);
            }
            return stats;
        }

        public void SessionHTTPRequest(object? sender, EventArgs e)
        {
            var context = (SessionContext?)sender;
            if (context?.Header?.Host != null)
            {
                Address host = context.Header.Host;
                if (host.Hostname.Length > 0)
                {
                    lock (_lock)
                    {
                        var hostStats = _hostStats.GetValueOrDefault(host.Hostname, new()
                        {
                            Count = 0,
                            BytesRead = 0,
                            BytesSent = 0
                        });
                        hostStats.Count += 1;
                        _hostStats[host.Hostname] = hostStats;
                    }
                }
            }
        }

        public void SessionBytesRead(object? sender, BytesReadEventArgs e)
        {
            var context = (SessionContext?)sender;
            if (context?.Header?.Host != null)
            {
                Address host = context.Header.Host;
                if (host.Hostname.Length > 0)
                {
                    lock (_lock)
                    {
                        HostStats? hostStats;
                        if (_hostStats.TryGetValue(host.Hostname, out hostStats))
                        {
                            hostStats.BytesRead += e.BytesRead;
                            _hostStats[host.Hostname] = hostStats;
                        }
                    }
                }
            }
        }

        public void SessionBytesSent(object? sender, BytesSentEventArgs e)
        {
            var context = (SessionContext?)sender;
            if (context?.Header?.Host != null)
            {
                Address host = context.Header.Host;
                if (host.Hostname.Length > 0)
                {
                    lock (_lock)
                    {
                        HostStats? hostStats;
                        if (_hostStats.TryGetValue(host.Hostname, out hostStats))
                        {
                            hostStats.BytesSent += e.BytesSent;
                            _hostStats[host.Hostname] = hostStats;
                        }
                    }
                }
            }
        }
    }
}
