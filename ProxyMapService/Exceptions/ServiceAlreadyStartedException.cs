namespace ProxyMapService.Exceptions
{
    public class ServiceAlreadyStartedException : Exception
    {
        public ServiceAlreadyStartedException() { }
        public ServiceAlreadyStartedException(string message) : base(message) { }
        public ServiceAlreadyStartedException(string message, Exception inner) : base(message, inner) { }
    }
}
