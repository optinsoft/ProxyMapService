namespace ProxyMapService.Proxy.Configurations
{
    public class HttpConfig
    {
        public string? UserAgent { get; set; }
        public bool EnableTranslateH1ToH2 { get; set; } = true;
        public bool DisableDowngradeToHttp1ForCache { get; set; } = false;
        public bool EnableDowngradeToHttp1ForOverwrittenHost { get; set; } = false;
        /// <summary>
        /// Global rules for editing request headers sent to the upstream server
        /// (can be extended at the Listen and HostRule levels, see HeadersRule).
        /// </summary>
        public HeadersRule RequestHeaders { get; set; } = new();
        /// <summary>
        /// Global rules for editing response headers sent back to the client
        /// (can be extended at the Listen and HostRule levels, see HeadersRule).
        /// </summary>
        public HeadersRule ResponseHeaders { get; set; } = new();
    }
}
