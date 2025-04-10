namespace ProxyMapService.Models
{
    public class HostStats
    {
        public int Count { get; set; }
        public bool Proxified { get; set; }
        public bool Bypassed { get; set; }
        public long BytesRead { get; set; }
        public long BytesSent { get; set; }
    }
}
