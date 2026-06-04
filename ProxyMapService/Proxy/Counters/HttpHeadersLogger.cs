namespace ProxyMapService.Proxy.Counters
{
    public class HttpHeadersLogger(bool response) : IHttpHeadersLogger
    {
        public void OnHttpHeader(object context, string[]? headers)
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
