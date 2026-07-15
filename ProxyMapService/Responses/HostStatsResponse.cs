namespace ProxyMapService.Responses
{
    public class HostStatsResponse
    {
#pragma warning disable IDE1006 // Naming Styles
        public required string hostName { get; set; }
        public required int requestsCount { get; set; }
        public required bool proxified { get; set; }
        public required bool bypassed { get; set; }
        public required long bytesRead { get; set; }
        public required long bytesSent { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}
