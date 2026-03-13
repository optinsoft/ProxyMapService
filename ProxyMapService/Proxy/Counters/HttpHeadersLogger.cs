using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public class HttpHeadersLogger(bool response) : IHttpHeadersLogger
    {
        public void OnHttpHeader(SessionContext context, string[]? headers)
        {
            HttpHeadersHandler?.Invoke(context, new()
            {
                Response = response,
                Headers = headers
            });
        }

        public event EventHandler<HttpHeadersEventArgs>? HttpHeadersHandler;
    }
}
