namespace ProxyMapService.WebLogging
{
    public class TrafficMonitorOptions
    {
        public bool Enabled { get; set; } = false;
        public int MaxEntries { get; set; } = 500;
    }
}
