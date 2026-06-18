namespace ProxyMapService.WebLogging
{
    public class EventLogOptions
    {
        public bool Enabled { get; set; } = false;
        public int MaxCount { get; set; } = 500;
    }
}
