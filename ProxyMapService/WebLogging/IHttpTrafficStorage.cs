using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public interface IHttpTrafficStorage
    {
        void AddEntry(WebSocketMessageEntry entry);
        HttpTrafficHistoryResponse GetRecentEntries();
        void Clear();
    }
}
