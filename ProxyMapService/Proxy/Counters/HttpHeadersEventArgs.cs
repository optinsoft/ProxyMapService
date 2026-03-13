namespace ProxyMapService.Proxy.Counters
{
    public class HttpHeadersEventArgs : EventArgs
    {
        public bool Response {  get; set; }
        public string[]? Headers { get; set; }
    }
}
