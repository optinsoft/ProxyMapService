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

            var id = e.Response ? loggersProvider.GetResponseBodyId() : loggersProvider.GetRequestBodyId();

            var bodyDto = HttpBodyParser.ParseBody(id, e.Completed, e.BodyLength, e.ContentType, e.ContentEncoding, e.BodyBytes ?? []);

            if (e.Response)
            {
                websocketLogService.QueueMessage(new HttpResponseBodyEntry(bodyDto));
            }
            else
            {
                websocketLogService.QueueMessage(new HttpRequestBodyEntry(bodyDto));
            }
        }

        void IHttpTrafficMonitor.LogHttpCompleted(object? sender, EventArgs e)
        {
            if (!loggingSwitch.IsHttpCapture) return;

            if (sender is not IHttpLoggersProvider loggersProvider) return;

            var id = loggersProvider.GetCompletionId();
            if (id == null) return;

            var completionDto = new HttpCompletionDto
            {
                Id = id,
            };
            websocketLogService.QueueMessage(new HttpCompletionEntry(completionDto));
        }

        void IHttpTrafficMonitor.LogHttpHeaders(object? sender, HttpHeadersEventArgs e)
        {
            if (!loggingSwitch.IsHttpCapture) return;

            if (sender is not IHttpLoggersProvider loggersProvider) return;

            var inbound = loggersProvider.GetInbound();
            var route = loggersProvider.GetRoute();
            var targetHost = loggersProvider.GetTargetHost();
            var completed = e.Completed;
            var headers = e.Headers;

            if (e.Response)
            {
                var id = loggersProvider.GetResponseId();
                if (HttpHeaderParser.ParseResponseRawHeaders(headers, id, completed) is HttpResponseDto responseDto)
                {
                    responseDto.Inbound = inbound;
                    responseDto.Route = route;
                    responseDto.TargetHost = targetHost;
                    websocketLogService.QueueMessage(new HttpResponseMessageEntry(responseDto));
                }
            }
            else
            {
                var id = loggersProvider.GetRequestId();
                if (HttpHeaderParser.ParseRequestRawHeaders(headers, id, completed) is HttpRequestDto requestDto)
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
