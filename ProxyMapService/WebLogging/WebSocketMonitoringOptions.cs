namespace ProxyMapService.WebLogging
{
    public class WebSocketMonitoringOptions
    {
        public EventLogOptions EventLog { get; set; } = new();
        public TrafficMonitorOptions TrafficMonitor { get; set; } = new();
        public int QueueCapacity { get; set; } = 10000;
    }
}
