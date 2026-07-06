using ProxyMapService.Proxy.Counters;

namespace ProxyMapService.WebLogging
{
    public class LoggingSwitch : IEventLoggingSwitch, IHttpLoggingSwitch
    {
        public bool IsEventCapture { get; set; }
        public bool IsHttpCapture { get; set; }
    }
}
