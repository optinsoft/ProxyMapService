using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Responses
{
    public class StatsResponse
    {
#pragma warning disable IDE1006 // Naming Styles
        public required string serviceInfo { get; set; }
        public required bool started { get; set; }
        public required string? startTime { get; set; }
        public required string? stopTime { get; set; }
        public required string currentTime { get; set; }
        public required int sessionsCount { get; set; }
        public required int authenticationNotRequired { get; set; }
        public required int authenticationRequired { get; set; }
        public required int authenticated { get; set; }
        public required int authenticationInvalid { get; set; }
        public required int httpRejected { get; set; }
        public required int headerFailed { get; set; }
        public required int noHost { get; set; }
        public required int hostRejected { get; set; }
        public required int hostProxified { get; set; }
        public required int hostBypassed { get; set; }
        public required int proxyConnected { get; set; }
        public required int proxyFailed { get; set; }
        public required int bypassConnected { get; set; }
        public required int bypassFailed { get; set; }
        public required long totalBytesRead { get; set; }
        public required long totalBytesSent { get; set; }
        public required long proxyBytesRead { get; set; }
        public required long proxyBytesSent { get; set; }
        public required long bypassBytesRead { get; set; }
        public required long bypassBytesSent { get; set; }
        public required int cacheResponses { get; set; }
        public required long cacheBytesSent { get; set; }
        public required bool logCapture { get; set; }
        public required bool httpCapture { get; set; }
        public required IEnumerable<PortRange> listenPorts { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}
