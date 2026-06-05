namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpLoggersProvider
    {
        IHttpHeadersLogger RequestHeadersLogger { get; }
        IHttpHeadersLogger ResponseHeadersLogger { get; }
        string GetRequestId();
        string GetResponseId();
        string? GetConnectionType();
        string? GetRoute();
    }
}
