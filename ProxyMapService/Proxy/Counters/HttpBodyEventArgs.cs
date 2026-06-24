namespace ProxyMapService.Proxy.Counters
{
    public class HttpBodyEventArgs
    {
        public bool Response { get; set; }
        public string? ContentType { get; set; }
        public long BodyLength { get; set; }
        public MemoryStream? BodyStream { get; set; }
    }
}
