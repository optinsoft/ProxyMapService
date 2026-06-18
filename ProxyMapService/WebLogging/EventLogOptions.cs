namespace ProxyMapService.WebLogging
{
    public class EventLogOptions
    {
        public bool Enabled { get; set; } = false;
        public int MaxEntries { get; set; } = 500;
    }
}
