namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyServer(string host, int port)
    {
        public string Host { get; private set; } = host;
        public int Port { get; private set; } = port;
    }
}
