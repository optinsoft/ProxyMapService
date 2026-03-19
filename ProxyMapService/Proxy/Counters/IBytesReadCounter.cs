using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public interface IBytesReadCounter
    {
        StreamDirection Direction { get; }
        long TotalBytesRead { get; }
        long ProxyBytesRead { get; }
        long BypassBytesRead { get; }
        long CachedBytesRead { get; }
        bool IsLogReading { get; }
        bool Cached { get; set; }
        void Reset();
        void OnBytesRead(SessionContext context, int bytesRead, byte[]? bytesData, int startIndex, long tunnelId);
    }
}
