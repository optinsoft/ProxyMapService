using ProxyMapService.Proxy.Utils;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Counters
{
    public class HttpHeadersLogger(bool response, IHttpLoggingSwitch? loggingSwitch) : IHttpHeadersLogger
    {
        public void OnHttpRequestHeader(object context, HttpRequestHeader? requestHeader)
        {
            if (loggingSwitch?.IsHttpCapture == false) return;
            if (requestHeader == null) return;
            bool completed = !HttpBodyUtils.RequestHasBody(requestHeader);
            HttpHeadersHandler?.Invoke(context, new()
            {
                HeaderId = requestHeader.RequestId,
                Response = false,
                Completed = completed,
                Headers = requestHeader.Headers
            });
        }

        public void OnHttpResponseHeader(object context, HttpResponseHeader? responseHeader,
            HttpRequestHeader? requestHeader)
        {
            if (loggingSwitch?.IsHttpCapture == false) return;
            if (responseHeader == null) return;
            bool completed = !HttpBodyUtils.ResponseHasBody(responseHeader, requestHeader?.HTTPVerb);
            HttpHeadersHandler?.Invoke(context, new()
            {
                HeaderId = requestHeader?.RequestId ?? responseHeader.ResponseId,
                Response = true,
                Completed = completed,
                Headers = responseHeader.Headers
            });
        }

        public void OnHttpResponseHeader(object context, HttpRequestHeader? requestHeader, 
            bool completed, string[]? headers)
        {
            if (loggingSwitch?.IsHttpCapture == false) return;
            if (requestHeader == null) return;
            HttpHeadersHandler?.Invoke(context, new()
            {
                HeaderId = requestHeader.RequestId,
                Response = true,
                Completed = completed,
                Headers = headers
            });
        }

        public event EventHandler<HttpHeadersEventArgs>? HttpHeadersHandler;
    }
}
