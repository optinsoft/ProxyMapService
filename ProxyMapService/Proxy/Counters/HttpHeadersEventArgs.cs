namespace ProxyMapService.Proxy.Counters
{
    public class HttpHeadersEventArgs : EventArgs
    {
        public required string HeaderId { get; set; }
        public bool Response {  get; set; }
        public bool Completed { get; set; }
        public string[]? Headers { get; set; }
    }
}
