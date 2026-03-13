namespace ProxyMapService.Proxy.Http
{
    public class HttpHeaderLinesAndBody
    {
        public required string[] HeaderLines;
        public byte[]? BodyBytes = null;
    }
}
