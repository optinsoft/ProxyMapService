namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpLoggersProvider
    {
        IHttpHeadersLogger RequestHeadersLogger { get; }
        IHttpHeadersLogger ResponseHeadersLogger { get; }
    }
}
