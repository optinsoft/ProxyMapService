namespace ProxyMapService.Proxy.Counters
{
    public class BytesSendEventArgs: EventArgs
    {
        public int BytesSend { get; set; }
        public byte[]? BytesData { get; set; }
        public int StartIndex { get; set; }
        public StreamDirection Direction { get; set; }
        public long TunnelId { get; set; }
    }
}
