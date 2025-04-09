﻿using Newtonsoft.Json;

namespace ProxyMapService.Responses
{
    public class StatsResponse
    {
#pragma warning disable IDE1006 // Naming Styles
        public string? serviceInfo { get; set; }
        public int? sessionsCount { get; set; }
        public int? authenticationNotRequired { get; set; }
        public int? authenticationRequired { get; set; }
        public int? authenticated { get; set; }
        public int? authenticationInvalid { get; set; }
        public int? httpRejected { get; set; }
        public int? connected { get; set; }
        public int? connectionFailed { get; set; }
        public int? headerFailed { get; set; }
        public int? HostFailed { get; set; }
        public long? totalBytesRead { get; set; }
        public long? totalBytesSent { get; set; }
        public List<HostStatsResponse>? hosts { get; set; }
#pragma warning restore IDE1006 // Naming Styles
    }
}
