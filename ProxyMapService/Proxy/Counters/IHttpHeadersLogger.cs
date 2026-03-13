using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpHeadersLogger
    {
        void OnHttpHeader(SessionContext context, string[]? headers);
    }
}
