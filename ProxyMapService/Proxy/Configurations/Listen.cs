namespace ProxyMapService.Proxy.Configurations
{
    public class Listen(int port, bool rejectHttpProxy)
    {
        public int Port { get; private set; } = port;
        public bool RejectHttpProxy { get; private set; } = rejectHttpProxy;
    }
}
