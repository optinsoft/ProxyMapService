using System.Net;

namespace ProxyMapService.Proxy.RoutingLoopDetector
{
    public class RoutingLoopDetector : IRoutingLoopDetector
    {
        private readonly HashSet<int> _listenPorts = new();
        private readonly HashSet<IPAddress> _localAddresses = new();

        public RoutingLoopDetector()
        {
        }

        public void AddListenPort(int port)
        { 
            _listenPorts.Add(port); 
        }

        public void ClearListenPorts()
        {
            _listenPorts.Clear();
        }

        public void InitLocalIpAddresses()
        {
            _localAddresses.Clear();

            _localAddresses.Add(IPAddress.Loopback);
            _localAddresses.Add(IPAddress.IPv6Loopback);
            _localAddresses.Add(IPAddress.Any);
            _localAddresses.Add(IPAddress.IPv6Any);

            string hostName = Dns.GetHostName();
            IPAddress[] ipAddresses = Dns.GetHostAddresses(hostName);

            foreach (var ip in ipAddresses)
            {
                _localAddresses.Add(ip);
            }
        }

        public bool IsSelfTargeting(IPAddress targetAddress, int targetPort)
        {
            if (!_listenPorts.Contains(targetPort))
            {
                return false;
            }
            return _localAddresses.Contains(targetAddress);
        }
    }
}
