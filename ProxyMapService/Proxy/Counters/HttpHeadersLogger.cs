using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

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

        public void OnHttpRequestHeader(object context, HttpRequestHeader requestHeader)
        {

            bool completed = requestHeader.HTTPVerb switch
            {
                "GET" => true,
                "DELETE" => true,
                "HEAD" => true,
                "OPTIONS" => true,
                "TRACE" => true,
                "CONNECT" => true,
                _ => !requestHeader.HasBody,
            };
            OnHttpHeader(context, completed, requestHeader.Headers);
        }

        public void OnHttpResponseHeader(object context, HttpResponseHeader responseHeader, HttpRequestHeader? requestHeader)
        {
            bool completed = requestHeader?.HTTPVerb == "HEAD" 
                || (responseHeader.StatusCode?.Length == 3 && (
                    responseHeader.StatusCode.StartsWith('1')
                    || responseHeader.StatusCode == "204"
                    || responseHeader.StatusCode == "304"))
                || !responseHeader.HasBody;
            OnHttpHeader(context, completed, responseHeader.Headers);
        }

        public event EventHandler<HttpHeadersEventArgs>? HttpHeadersHandler;
    }
}
