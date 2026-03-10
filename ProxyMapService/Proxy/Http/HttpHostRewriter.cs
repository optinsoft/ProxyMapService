using Newtonsoft.Json.Linq;
using System.Text;

namespace ProxyMapService.Proxy.Http
{
    public class HttpHostRewriter
    {
        public static bool OverrideHostHeader(string[] lines, string hostname, int port)
        {
            bool hostFound = false;

            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"Host: {hostname}:{port}";
                    hostFound = true;
                    break;
                }
            }

            return hostFound;
        }
    }
}
