using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public interface IBytesReadCounter
    {
        string Direction { get; }
        long TotalBytesRead { get; }
        long ProxyBytesRead { get; }
        long BypassBytesRead { get; }
        void Reset();
        void OnBytesRead(SessionContext context, int bytesRead, byte[]? bytesData, int startIndex);
    }
}
