using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Counters
{
    public class HttpCompletionLogger(IHttpLoggingSwitch? loggingSwitch) : IHttpCompletionLogger
    {
        public void OnHttpCompleted(object context, HttpRequestHeader? requestHeader)
        {
            if (loggingSwitch?.IsHttpCapture == false) return;
            if (requestHeader == null) return;
            HttpCompletionHandler?.Invoke(context, new()
            {
                HeaderId = requestHeader.RequestId
            });
        }

        public event EventHandler<HttpCompletedEventArgs>? HttpCompletionHandler;
    }
}
