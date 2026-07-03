namespace ProxyMapService.Proxy.Counters
{
    public class HttpBodyEventArgs
    {
        public bool Response { get; set; }
        public bool Completed { get; set; }
        public string? ContentType { get; set; }
        public string? ContentEncoding { get; set; }
        public long BodyLength { get; set; }
        public byte[]? BodyBytes { get; set; }
    }
}
