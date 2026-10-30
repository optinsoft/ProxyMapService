namespace ProxyMapService.Proxy.Configurations
{
    public class HttpConfig
    {
        public string? UserAgent { get; set; }
        public bool EnableTranslateH1ToH2 { get; set; } = true;
        public bool DisableDowngradeToHttp1ForCache { get; set; } = false;
        public bool DisableDowngradeToHttp1ForOverwrittenHost { get; set; } = false;
    }
}
