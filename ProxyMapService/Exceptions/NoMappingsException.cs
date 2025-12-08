namespace ProxyMapService.Exceptions
{
    public class NoMappingsException : Exception
    {
        public NoMappingsException() { }
        public NoMappingsException(string message) : base(message) { }
        public NoMappingsException(string message, Exception inner) : base(message, inner) { }
    }
}
