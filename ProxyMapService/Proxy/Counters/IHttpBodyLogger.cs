namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpBodyLogger
    {
        void OnCompleted(object context, string? contentType, string? contentEncoding, long bodyLength, byte[] bodyBytes);
        void OnCompleted(object context, string? contentType, long bodyLength, byte[] bodyBytes);
        void OnCompleted(object context, string? contentType, string? contentEncoding, long bodyLength, MemoryStream? bodyStream);
        void OnCompleted(object context, string? contentType, long bodyLength, MemoryStream? bodyStream);
    }
}
