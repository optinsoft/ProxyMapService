namespace ProxyMapService.Proxy.Configurations
{
    public class HttpConfig
    {
        public string? UserAgent { get; set; }
        public bool EnableTranslateH1ToH2 { get; set; } = false;
    }
}
