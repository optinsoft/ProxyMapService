namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpCompletionLogger
    {
        void OnHttpCompleted(object context);
    }
}
