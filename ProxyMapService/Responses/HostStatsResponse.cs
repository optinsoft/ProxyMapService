namespace ProxyMapService.Responses
{
    public class HostStatsResponse
    {
#pragma warning disable IDE1006 // Naming Styles
        public required string hostName { get; set; }
        public int requestsCount { get; set; }
        public long? bytesRead { get; set; }
        public long? bytesSent { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}
