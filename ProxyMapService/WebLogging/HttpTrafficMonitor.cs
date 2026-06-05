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
            if (!currentOptions.Enabled || !currentOptions.TrafficMonitor.Enabled)
            {
                return;
            }

            if (sender is not IHttpLoggersProvider loggersProvider)
            {
                return;
            }

            var route = loggersProvider.GetRoute();
            var headers = e.Headers;

            if (!e.Response)
            {
                if (HttpHeaderParser.ParseRequestRawHeaders(headers, route) is HttpRequestDto requestDto)
                {
                    websocketLogService.QueueMessage(new HttpRequestMessageEntry(requestDto));
                }
            }
            else
            {
                if (HttpHeaderParser.ParseResponseRawHeaders(headers, route) is HttpResponseDto responseDto)
                {
                    websocketLogService.QueueMessage(new HttpResponseMessageEntry(responseDto));
                }
            }
        }
    }
}
