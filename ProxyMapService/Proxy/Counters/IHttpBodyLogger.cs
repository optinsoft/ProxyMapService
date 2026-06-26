namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpBodyLogger
    {
        void OnCompleted(object context, string? contentType, long bodyLength, byte[] bodyBytes);
        void OnCompleted(object context, string? contentType, long bodyLength, MemoryStream? bodyStream);
    }
}
