namespace ProxyMapService.WebLogging
{
    public interface IEventLoggingSwitch
    {
        bool IsEventCapture { get; set; }
    }
}
