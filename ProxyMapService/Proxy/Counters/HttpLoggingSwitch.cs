namespace ProxyMapService.Proxy.Counters
{
    public class HttpLoggingSwitch : IHttpLoggingSwitch
    {
        public bool IsHttpCapture { get; set; }
    }
}
