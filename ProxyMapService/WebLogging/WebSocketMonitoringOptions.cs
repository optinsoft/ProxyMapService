namespace ProxyMapService.WebLogging
{
    public class WebSocketMonitoringOptions
    {
        public bool Enabled { get; set; } = false;
        public int QueueCapacity { get; set; } = 10000;
        public TrafficMonitorOptions TrafficMonitor { get; set; } = new();
    }
}
