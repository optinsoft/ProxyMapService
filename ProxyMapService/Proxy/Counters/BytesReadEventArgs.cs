namespace ProxyMapService.Proxy.Counters
{
    public class BytesReadEventArgs: EventArgs
    {
        public int BytesRead { get; set; }
        public byte[]? BytesData { get; set; }
        public int StartIndex { get; set; }
    }
}
