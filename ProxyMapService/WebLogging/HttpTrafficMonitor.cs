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

            var inbound = loggersProvider.GetInbound();
            var route = loggersProvider.GetRoute();
            var targetHost = loggersProvider.GetTargetHost();
            var headers = e.Headers;

            if (!e.Response)
            {
                var id = loggersProvider.GetRequestId();
                if (HttpHeaderParser.ParseRequestRawHeaders(headers, id) is HttpRequestDto requestDto)
                {
                    requestDto.Inbound = inbound;
                    requestDto.Route = route;
                    requestDto.TargetHost = targetHost;
                    websocketLogService.QueueMessage(new HttpRequestMessageEntry(requestDto));
                }
            }
            else
            {
                var id = loggersProvider.GetResponseId();
                if (HttpHeaderParser.ParseResponseRawHeaders(headers, id) is HttpResponseDto responseDto)
                {
                    responseDto.Inbound = inbound;
                    responseDto.Route = route;
                    responseDto.TargetHost = targetHost;
                    websocketLogService.QueueMessage(new HttpResponseMessageEntry(responseDto));
                }
            }
        }
    }
}
