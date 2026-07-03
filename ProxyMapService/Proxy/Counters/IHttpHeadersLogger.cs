namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpHeadersLogger
    {
        void OnHttpHeader(object context, bool completed, string[]? headers);
    }
}
