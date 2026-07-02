using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public abstract record WebSocketMessageEntry;

    public record LogMessageEntry(
        DateTime Timestamp,
        string Category,
        string Level,
        string Message,
        string? Exception
    ) : WebSocketMessageEntry;

    public record HttpRequestMessageEntry(HttpRequestDto Dto) : WebSocketMessageEntry;

    public record HttpResponseMessageEntry(HttpResponseDto Dto) : WebSocketMessageEntry;

    public record HttpRequestBodyEntry(HttpBodyDto Dto) : WebSocketMessageEntry;

    public record HttpResponseBodyEntry(HttpBodyDto Dto) : WebSocketMessageEntry;

    public record HttpCompletionEntry(HttpCompletionDto Dto) : WebSocketMessageEntry;

    public record StatsMessageEntry (
        string ServiceInfo,
        bool Started,
        string? StartTime,
        string? StopTime,
        string CurrentTime,
        int SessionsCount,
        int AuthenticationNotRequired,
        int AuthenticationRequired,
        int Authenticated,
        int AuthenticationInvalid,
        int HttpRejected,
        int HeaderFailed,
        int NoHost,
        int HostRejected,
        int HostProxified,
        int HostBypassed,
        int ProxyConnected,
        int ProxyFailed,
        int BypassConnected,
        int BypassFailed,
        long TotalBytesRead,
        long TotalBytesSent,
        long ProxyBytesRead,
        long ProxyBytesSent,
        long BypassBytesRead,
        long BypassBytesSent,
        int CacheResponses,
        long CacheBytesSent
    ) : WebSocketMessageEntry;
}
