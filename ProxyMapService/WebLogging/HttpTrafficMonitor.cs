using Microsoft.Extensions.Options;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Services;
using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public class HttpTrafficMonitor(
        WebSocketLogBackgroundService websocketLogService,
        IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : IHttpTrafficMonitor
    {
        void IHttpTrafficMonitor.LogHttpHeaders(object? sender, HttpHeadersEventArgs e)
        {
            var currentOptions = monitoringOptions.CurrentValue;
            if (!currentOptions.TrafficMonitor.Enabled)
            {
                return;
            }

            if (sender is not IHttpLoggersProvider loggersProvider)
            {
                return;
            }

            var connectionType = loggersProvider.GetConnectionType();
            var route = loggersProvider.GetRoute();
            var headers = e.Headers;

            if (!e.Response)
            {
                var id = loggersProvider.GetRequestId();
                if (HttpHeaderParser.ParseRequestRawHeaders(headers, id, connectionType, route) is HttpRequestDto requestDto)
                {
                    websocketLogService.QueueMessage(new HttpRequestMessageEntry(requestDto));
                }
            }
            else
            {
                var id = loggersProvider.GetResponseId();
                if (HttpHeaderParser.ParseResponseRawHeaders(headers, id, connectionType, route) is HttpResponseDto responseDto)
                {
                    websocketLogService.QueueMessage(new HttpResponseMessageEntry(responseDto));
                }
            }
        }
    }
}
