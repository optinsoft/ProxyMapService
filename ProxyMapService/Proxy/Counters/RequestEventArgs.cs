namespace ProxyMapService.Proxy.Counters
{
    public class RequestEventArgs
    {
        public required string Host { get; set; }
        public int Port { get; set; }
    }
}
