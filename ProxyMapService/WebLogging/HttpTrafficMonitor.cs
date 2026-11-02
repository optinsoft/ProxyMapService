using ProxyMapService.Proxy.Counters;
using ProxyMapService.Services;
using ProxyMapService.WebLogging.Dtos;

namespace ProxyMapService.WebLogging
{
    public class HttpTrafficMonitor(
        WebSocketLogBackgroundService websocketLogService,
        IHttpLoggingSwitch loggingSwitch) : IHttpTrafficMonitor
    {
        void IHttpTrafficMonitor.LogHttpBody(object? sender, HttpBodyEventArgs e)
        {
            if (!loggingSwitch.IsHttpCapture) return;

            if (sender is not IHttpLoggersProvider loggersProvider) return;
            if (e.BodyLength == 0) return;

            var bodyDto = HttpBodyParser.ParseBody(
                e.HeaderId, e.Response, e.Completed, e.BodyLength, 
                e.ContentType, e.ContentEncoding, e.RequestXMicrosoftAjax,
                e.BodyBytes ?? []);

            if (e.Response)
            {
                websocketLogService.QueueMessage(new HttpResponseBodyEntry(bodyDto));
            }
            else
            {
                websocketLogService.QueueMessage(new HttpRequestBodyEntry(bodyDto));
            }
        }

        void IHttpTrafficMonitor.LogHttpCompleted(object? sender, HttpCompletedEventArgs e)
        {
            if (!loggingSwitch.IsHttpCapture) return;

            if (sender is not IHttpLoggersProvider loggersProvider) return;

            var completionDto = new HttpCompletionDto
            {
                Id = e.HeaderId,
            };
            websocketLogService.QueueMessage(new HttpCompletionEntry(completionDto));
        }

        void IHttpTrafficMonitor.LogHttpHeaders(object? sender, HttpHeadersEventArgs e)
        {
            if (!loggingSwitch.IsHttpCapture) return;

            if (sender is not IHttpLoggersProvider loggersProvider) return;

            var inbound = loggersProvider.Inbound;
            var route = loggersProvider.Route;
            var targetHost = loggersProvider.TargetHost;            
            var clientHttp2 = loggersProvider.ClientHttp2;
            var targetHttp2 = loggersProvider.TargetHttp2;
            var completed = e.Completed;
            var headers = e.Headers;

            if (e.Response)
            {
                if (HttpHeaderParser.ParseResponseRawHeaders(headers, e.HeaderId, completed) is HttpResponseDto responseDto)
                {
                    responseDto.Inbound = inbound;
                    responseDto.Route = route;
                    responseDto.TargetHost = targetHost;
                    websocketLogService.QueueMessage(new HttpResponseMessageEntry(responseDto));
                }
            }
            else
            {
                if (HttpHeaderParser.ParseRequestRawHeaders(headers, e.HeaderId, completed, clientHttp2, targetHttp2) is HttpRequestDto requestDto)
                {
                    requestDto.Inbound = inbound;
                    requestDto.Route = route;
                    requestDto.TargetHost = targetHost;
                    websocketLogService.QueueMessage(new HttpRequestMessageEntry(requestDto));
                }
            }
        }
    }
}
