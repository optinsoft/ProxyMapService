namespace ProxyMapService.Proxy.Exceptions
{
    public class NullClientStreamException : Exception
    {
        public NullClientStreamException() { }
        public NullClientStreamException(string message) : base(message) { }
        public NullClientStreamException(string message, Exception inner) : base(message, inner) { }
    }
}
