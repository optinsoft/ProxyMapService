namespace ProxyMapService.Proxy.Exceptions
{
    public class NullHeaderBytesException : Exception
    {
        public NullHeaderBytesException() { }
        public NullHeaderBytesException(string message) : base(message) { }
        public NullHeaderBytesException(string message, Exception inner) : base(message, inner) { }
    }
}
