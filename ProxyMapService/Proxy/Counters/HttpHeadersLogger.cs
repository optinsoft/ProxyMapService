namespace ProxyMapService.Proxy.Counters
{
    public class HttpHeadersLogger(bool response) : IHttpHeadersLogger
    {
        public void OnHttpHeader(object context, bool completed, string[]? headers)
        {
            HttpHeadersHandler?.Invoke(context, new()
            {
                Response = response,
                Completed = completed,
                Headers = headers
            });
        }

        public event EventHandler<HttpHeadersEventArgs>? HttpHeadersHandler;
    }
}
