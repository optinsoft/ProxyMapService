namespace ProxyMapService.Proxy.Exceptions
{
    public class NullServerCertificateException : Exception
    {
        public NullServerCertificateException() { }
        public NullServerCertificateException(string message) : base(message) { }
        public NullServerCertificateException(string message, Exception inner) : base(message, inner) { }
    }
}
