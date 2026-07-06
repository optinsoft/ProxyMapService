namespace ProxyMapService.Proxy.Counters
{
    public class HttpCompletionLogger(IHttpLoggingSwitch? loggingSwitch) : IHttpCompletionLogger
    {
        public void OnHttpCompleted(object context)
        {
            if (loggingSwitch?.IsHttpCapture == false) return;
            HttpCompletionHandler?.Invoke(context, new EventArgs());
        }

        public event EventHandler<EventArgs>? HttpCompletionHandler;
    }
}
