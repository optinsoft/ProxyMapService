using ProxyMapService.Proxy.Network;

namespace ProxyMapService.Proxy.Configurations
{
    public class ProxyServer(string host, int port, ProxyType proxyType = ProxyType.Http, string username = "", string password = "")
    {
        public string Host { get; private set; } = host;
        public int Port { get; private set; } = port;
        public ProxyType ProxyType { get; private set; } = proxyType;
        public string Username { get; private set; } = username;
        public string Password { get; private set; } = password;
        public UsernameParameterList UsernameParameters { get; init; } = new();
    }
}
