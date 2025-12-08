namespace ProxyMapService.Proxy.Exceptions
{
    public class NoProxyServerException : Exception
    {
        public NoProxyServerException() { }
        public NoProxyServerException(string message) : base(message) { }
        public NoProxyServerException(string message, Exception inner) : base(message, inner) { }
    }
}
