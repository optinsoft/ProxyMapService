using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public interface IBytesReadCounter
    {
        StreamDirection Direction { get; }
        long TotalBytesRead { get; }
        long ProxyBytesRead { get; }
        long BypassBytesRead { get; }
        bool IsLogReading { get; }
        void Reset();
        void OnBytesRead(SessionContext context, int bytesRead, byte[]? bytesData, int startIndex);
    }
}
