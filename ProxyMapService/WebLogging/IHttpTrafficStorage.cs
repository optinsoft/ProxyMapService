using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public interface IHttpTrafficStorage
    {
        void AddEntry(WebSocketMessageEntry entry);
        HttpTrafficHistoryDto GetRecentEntries();
        void Clear();
    }
}
