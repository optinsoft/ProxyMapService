using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpHeadersLogger
    {
        void OnHttpHeader(object context, bool completed, string[]? headers);
        void OnHttpHeader(object context, HttpRequestHeader requestHeader);
        void OnHttpHeader(object context, HttpResponseHeader responseHeader);
    }
}
