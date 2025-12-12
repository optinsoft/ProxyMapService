using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Exceptions;

namespace ProxyMapService.Proxy.Provider
{
    public class ProxyProvider(List<ProxyServer> proxyServers) : IProxyProvider
    {
        private readonly List<ProxyServer> _proxyServers = Shuffle(proxyServers);
        private int _currentProxy = 0;
        
        public ProxyServer GetProxyServer()
        {
            if (_proxyServers.Count == 0)
            {
                throw new NoProxyServerException();
            }
            uint proxyCount = (uint)_proxyServers.Count;
            uint proxyIndex = (uint)Interlocked.Add(ref _currentProxy, 1) % proxyCount;
            return _proxyServers[(int)proxyIndex];
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
