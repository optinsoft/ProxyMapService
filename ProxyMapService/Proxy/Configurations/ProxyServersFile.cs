using System.IO;

namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyServersFile(string path)
    {
        public string Path { get; init; } = path;
    }
}
