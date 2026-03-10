using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Network;
using System.Text;

namespace ProxyMapService.Proxy.Http
{
    public class HttpHostRewriter
    {
        public static bool OverrideHostHeader(string[] lines, HostAddress host)
        {
            bool hostFound = false;

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"Host: {host.Hostname}:{host.Port}";
                    hostFound = true;
                    break;
                }
            }

            return hostFound;
        }
    }
}
