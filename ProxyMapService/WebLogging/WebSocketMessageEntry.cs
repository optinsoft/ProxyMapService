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
}
