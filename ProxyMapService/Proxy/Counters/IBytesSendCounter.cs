using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public interface IBytesSendCounter
    {
        StreamDirection Direction { get; }
        long TotalBytesSent { get; }
        long ProxyBytesSent { get; }
        long BypassBytesSent { get; }
        long CacheBytesSent { get; }
        bool IsLogSending {  get; }
        void Reset();
        void OnBytesSend(SessionContext context, int bytesSend, byte[]? bytesData, int startIndex, long tunnelId);
        void OnBytesSent(SessionContext context, int bytesSent, byte[]? bytesData, int startIndex, long tunnelId);
    }
}
