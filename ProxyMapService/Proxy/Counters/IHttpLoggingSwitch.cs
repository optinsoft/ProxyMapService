namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpLoggingSwitch
    {
        bool IsHttpCapture { get; set; }
    }
}
