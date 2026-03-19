namespace ProxyMapService.Proxy.Cache
{
    public class CacheEntry
    {
        public string Key { get; set; } = default!;
        public string Host { get; set; } = default!;
        public string Url { get; set; } = default!;
        public string? ETag { get; set; }
        public string FilePath { get; set; } = default!;
        public int HeaderLength { get; set; }
        public long ContentLength { get; set; }
        public string? ContentType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccess { get; set; }
    }
}
