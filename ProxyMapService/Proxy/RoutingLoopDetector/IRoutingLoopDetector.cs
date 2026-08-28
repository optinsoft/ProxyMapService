using System.Net;

namespace ProxyMapService.Proxy.RoutingLoopDetector
{
    public interface IRoutingLoopDetector
    {
        void AddListenPort(int port);
        void ClearListenPorts();
        void InitLocalIpAddresses();
        bool IsSelfTargeting(IPAddress targetAddress, int targetPort);
    }
}
