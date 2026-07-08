using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public interface IHttpTrafficStorage
    {
        void AddRequestEntry(HttpRequestMessageEntry entry);
        void AddResponseEntry(HttpResponseMessageEntry entry);
        void AddCompletionEntry(HttpCompletionEntry entry);
        void AddRequestBodyEntry(HttpRequestBodyEntry entry);
        void AddResponseBodyEntry(HttpResponseBodyEntry entry);
        HttpTrafficHistoryDto GetRecentEntries();
        void Clear();
    }
}
