namespace ProxyMapService.Proxy.Counters
{
    public interface IHttpLoggersProvider
    {
        IHttpHeadersLogger? RequestHeadersLogger { get; }
        IHttpHeadersLogger? ResponseHeadersLogger { get; }
        IHttpCompletionLogger? CompletionLogger { get; }
        IHttpBodyLogger? RequestBodyLogger { get; }
        IHttpBodyLogger? ResponseBodyLogger { get; }
        string? Inbound {  get; }
        string? Route { get; }
        string? TargetHost { get; }
        bool ClientHttp2 { get; }
        bool TargetHttp2 { get; }
    }
}
