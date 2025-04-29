namespace ProxyMapService.Proxy.Counters
{
    public class BytesSentEventArgs: EventArgs
    {
        public int BytesSent { get; set; }
        public byte[]? BytesData { get; set; }
        public int StartIndex { get; set; }
    }
}
