using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpCompletionLogger
    {
        void OnHttpCompleted(object context, HttpRequestHeader? requestHeader);
    }
}
