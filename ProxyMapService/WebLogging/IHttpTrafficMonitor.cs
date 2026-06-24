using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.WebLogging
{
    public interface IHttpTrafficMonitor
    {
        void LogHttpHeaders(object? sender, HttpHeadersEventArgs e);
        void LogHttpBody(object? sender, HttpBodyEventArgs e);
    }
}
