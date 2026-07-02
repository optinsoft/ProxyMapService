namespace ProxyMapService.Proxy.Counters
{
    public class HttpCompletionLogger : IHttpCompletionLogger
    {
        public void OnHttpCompleted(object context)
        {
            HttpCompletionHandler?.Invoke(context, new EventArgs());
        }

        public event EventHandler<EventArgs>? HttpCompletionHandler;
    }
}
