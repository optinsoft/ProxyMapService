using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public interface IBytesSentCounter
    {
        StreamDirection Direction { get; }
        long TotalBytesSent { get; }
        long ProxyBytesSent { get; }
        long BypassBytesSent { get; }
        bool IsLogSending {  get; }
        void Reset();
        void OnBytesSent(SessionContext context, int bytesSent, byte[]? bytesData, int startIndex);
    }
}
