using ProxyMapService.Proxy.Network;

namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyServer(string host, int port)
    {
        public string Host { get; init; } = host;
        public int Port { get; init; } = port;
        public ProxyType ProxyType { get; set; } = ProxyType.Http;
        public string Username { get; set; } = String.Empty;
        public string Password { get; set; } = String.Empty;
        public bool ResolveIP { get; set; }
        public UsernameParameterList UsernameParameters { get; init; } = new();
    }
}
