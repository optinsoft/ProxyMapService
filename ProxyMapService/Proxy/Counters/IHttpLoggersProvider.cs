namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpLoggersProvider
    {
        IHttpHeadersLogger? RequestHeadersLogger { get; }
        IHttpHeadersLogger? ResponseHeadersLogger { get; }
        IHttpCompletionLogger? CompletionLogger { get; }
        IHttpBodyLogger? RequestBodyLogger { get; }
        IHttpBodyLogger? ResponseBodyLogger { get; }
        string GetRequestId();
        string GetResponseId();
        string? GetCompletionId();
        string GetRequestBodyId();
        string GetResponseBodyId();
        string? GetInbound();
        string? GetRoute();
        string? GetTargetHost();
    }
}
