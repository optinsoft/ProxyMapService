namespace ProxyMapService.Proxy.Configurations
{
    public class CacheConfig
    {
        public bool Enabled { get; set; }
        public string DbPath { get; set; } = string.Empty;
        public string CacheDir {  get; set; } = string.Empty;
    }
}
