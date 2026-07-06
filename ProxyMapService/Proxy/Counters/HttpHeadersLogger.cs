using Microsoft.VisualBasic;
using ProxyMapService.Proxy.Headers;

namespace ProxyMapService.Proxy.Counters
{
    public class HttpHeadersLogger(bool response, IHttpLoggingSwitch? loggingSwitch) : IHttpHeadersLogger
    {
        public void OnHttpHeader(object context, bool completed, string[]? headers)
        {
            if (loggingSwitch?.IsHttpCapture == false) return;
            HttpHeadersHandler?.Invoke(context, new()
            {
                Response = response,
                Completed = completed,
                Headers = headers
            });
        }

        public void OnHttpHeader(object context, HttpRequestHeader requestHeader)
        {
           OnHttpHeader(context, requestHeader.ContentLength.HasValue && requestHeader.ContentLength.Value == 0, requestHeader.Headers);
        }

        public void OnHttpHeader(object context, HttpResponseHeader responseHeader)
        {
            OnHttpHeader(context, responseHeader.ContentLength.HasValue && responseHeader.ContentLength.Value == 0, responseHeader.Headers);
        }

        public event EventHandler<HttpHeadersEventArgs>? HttpHeadersHandler;
    }
}
