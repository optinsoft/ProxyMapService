using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpBodyLogger
    {
        void OnCompleted(object context, HttpRequestHeader? requestHeader, string? contentType, string? contentEncoding, long bodyLength, byte[] bodyBytes);
        void OnCompleted(object context, HttpRequestHeader? requestHeader, string? contentType, long bodyLength, byte[] bodyBytes);
        void OnCompleted(object context, HttpRequestHeader? requestHeader, string? contentType, string? contentEncoding, long bodyLength, MemoryStream? bodyStream);
        void OnCompleted(object context, HttpRequestHeader? requestHeader, string? contentType, long bodyLength, MemoryStream? bodyStream);
    }
}
