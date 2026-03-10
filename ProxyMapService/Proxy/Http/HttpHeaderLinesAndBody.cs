namespace ProxyMapService.Proxy.Http
{
    public class HttpHeaderLinesAndBody
    {
        public required string[] headerLines;
        public byte[]? bodyBytes = null;
    }
}
