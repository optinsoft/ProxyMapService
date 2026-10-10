using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpHeadersLogger
    {
        void OnHttpHeader(object context, bool completed, string[]? headers);
        void OnHttpRequestHeader(object context, HttpRequestHeader requestHeader);
        void OnHttpResponseHeader(object context, HttpResponseHeader responseHeader, HttpRequestHeader? requestHeader);
    }
}
