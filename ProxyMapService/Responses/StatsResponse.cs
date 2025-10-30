using Newtonsoft.Json;

namespace ProxyMapService.Responses
{
    public class StatsResponse
    {
#pragma warning disable IDE1006 // Naming Styles
        public bool? started { get; set; }
        public string? serviceInfo { get; set; }
        public string? currentTime { get; set; }
        public int? sessionsCount { get; set; }
        public int? authenticationNotRequired { get; set; }
        public int? authenticationRequired { get; set; }
        public int? authenticated { get; set; }
        public int? authenticationInvalid { get; set; }
        public int? httpRejected { get; set; }
        public int? headerFailed { get; set; }
        public int? noHost { get; set; }
        public int? hostRejected { get; set; }
        public int? hostProxified { get; set; }
        public int? hostBypassed { get; set; }
        public int? proxyConnected { get; set; }
        public int? proxyFailed { get; set; }
        public int? bypassConnected { get; set; }
        public int? bypassFailed { get; set; }
        public long? totalBytesRead { get; set; }
        public long? totalBytesSent { get; set; }
        public long? proxyBytesRead { get; set; }
        public long? proxyBytesSent { get; set; }
        public long? bypassBytesRead { get; set; }
        public long? bypassBytesSent { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}
