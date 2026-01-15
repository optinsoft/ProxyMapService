using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Sessions;
using System.Collections.Concurrent;

namespace ProxyMapService.Proxy.Providers
{
    public class ProxyProvider(List<ProxyServer> proxyServers) : IProxyProvider
    {
        private readonly List<ProxyServer> _proxyServers = Shuffle(proxyServers);
        private int _currentProxy = 0;
        private readonly ConcurrentDictionary<string, ProxySession> _sessions = new();
        private readonly PriorityQueue<(string id, long version), DateTime> _queue = new();

        private long _versionCounter = 0;
        private readonly object _queueLock = new();

        private ProxySession AddSession(string sessionId, int sessionTime, ProxyServer proxyServer)
        {
            CleanupExpiredSessions();

            var version = Interlocked.Increment(ref _versionCounter);
            var session = new ProxySession(sessionId, sessionTime, proxyServer, version);

            _sessions[sessionId] = session; // thread-safe

            lock (_queueLock)
            {
                _queue.Enqueue((sessionId, version), session.ExpireTime);
            }

            return session;
        }

        private ProxySession? GetSession(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return null;
            
            if (session.IsExpired)
            {
                _sessions.TryRemove(sessionId, out _);
                return null;
            }

            return session;
        }

        public void CleanupExpiredSessions()
        {
            var now = DateTime.Now;

            while (true)
            {
                (string id, long version) top;

                lock (_queueLock)
                {
                    if (!_queue.TryPeek(out top, out var expireTime))
                        return;

                    if (expireTime > now)
                        return;

                    _queue.Dequeue();
                }

                if (_sessions.TryGetValue(top.id, out var session))
                {
                    if (session.Version == top.version && session.IsExpired)
                    {
                        _sessions.TryRemove(top.id, out _);
                    }
                }
            }
        }

        public ProxyServer GetProxyServer(SessionContext context)
        {
            if (_proxyServers.Count == 0)
            {
                throw new NoProxyServerException();
            }
            if (context.SessionId != null)
            {
                var session = GetSession(context.SessionId);
                if (session != null)
                {
                    return session.ProxyServer;
                }
            }
            uint proxyCount = (uint)_proxyServers.Count;
            uint proxyIndex = (uint)Interlocked.Add(ref _currentProxy, 1) % proxyCount;
            var proxyServer = _proxyServers[(int)proxyIndex];
            if (context.SessionId != null && context.SessionTime > 0)
            {
                AddSession(context.SessionId, context.SessionTime, proxyServer);
            }
            return proxyServer;
        }

        private static List<ProxyServer> Shuffle(List<ProxyServer> source)
        {
            var list = new List<ProxyServer>(source);
            var rnd = Random.Shared;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list;
        }
    }
}
