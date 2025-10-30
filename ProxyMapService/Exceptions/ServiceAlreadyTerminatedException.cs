namespace ProxyMapService.Exceptions
{
    public class ServiceAlreadyTerminatedException: Exception
    {
        public ServiceAlreadyTerminatedException() { }
        public ServiceAlreadyTerminatedException(string message) : base(message) { }
        public ServiceAlreadyTerminatedException(string message, Exception inner) : base(message, inner) { }
    }
}
