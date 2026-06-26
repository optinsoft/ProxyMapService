using ProxyMapService.Proxy.Counters;
using ProxyMapService.Services;
using ProxyMapService.WebLogging.Dtos;
using System.Text;

namespace ProxyMapService.WebLogging
{
    public class HttpTrafficMonitor(
        WebSocketLogBackgroundService websocketLogService) : IHttpTrafficMonitor
    {
        void IHttpTrafficMonitor.LogHttpBody(object? sender, HttpBodyEventArgs e)
        {
            if (sender is not IHttpLoggersProvider loggersProvider)
            {
                return;
            }

            if (e.BodyLength == 0)
            {
                return;
            }

            var id = e.Response ? loggersProvider.GetResponseBodyId() : loggersProvider.GetRequestBodyId();
            var contentKind = GetContentKind(e.ContentType);

            var bodyDto = new HttpBodyDto
            {
                Id = id,
                Length = e.BodyLength,
                ContentType = e.ContentType,
                ContentKind = contentKind
            };

            var bytes = e.BodyStream?.ToArray() ?? [];

            switch (contentKind)
            {
                case HttpBodyContentKind.Json:
                    bodyDto.Content = Encoding.UTF8.GetString(bytes);
                    break;

                case HttpBodyContentKind.Xml:
                    bodyDto.Content = Encoding.UTF8.GetString(bytes);
                    break;

                case HttpBodyContentKind.Html:
                    bodyDto.Content = Encoding.UTF8.GetString(bytes);
                    break;

                case HttpBodyContentKind.Text:
                    bodyDto.Content = Encoding.UTF8.GetString(bytes);
                    break;

                case HttpBodyContentKind.FormUrlEncoded:
                    bodyDto.Content = Encoding.UTF8.GetString(bytes);
                    break;

                case HttpBodyContentKind.Image:
                    bodyDto.BinaryContentBase64 = Convert.ToBase64String(bytes);
                    break;

                default:
                    bodyDto.BinaryContentBase64 = Convert.ToBase64String(bytes);
                    break;
            }

            if (e.Response)
            {
                websocketLogService.QueueMessage(new HttpResponseBodyEntry(bodyDto));
            }
            else
            {
                websocketLogService.QueueMessage(new HttpRequestBodyEntry(bodyDto));
            }
        }

        void IHttpTrafficMonitor.LogHttpHeaders(object? sender, HttpHeadersEventArgs e)
        {
            if (sender is not IHttpLoggersProvider loggersProvider)
            {
                return;
            }

            var inbound = loggersProvider.GetInbound();
            var route = loggersProvider.GetRoute();
            var targetHost = loggersProvider.GetTargetHost();
            var headers = e.Headers;

            if (e.Response)
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
            else
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
        }
        
        private static HttpBodyContentKind GetContentKind(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return HttpBodyContentKind.Binary;
            }

            var mediaType = contentType
                .Split(';', 2)[0]
                .Trim()
                .ToLowerInvariant();

            if (mediaType.Contains("json"))
            {
                return HttpBodyContentKind.Json;
            }

            if (mediaType == "application/x-www-form-urlencoded")
            {
                return HttpBodyContentKind.FormUrlEncoded;
            }
            
            if (mediaType.StartsWith("multipart/form-data"))
            {
                return HttpBodyContentKind.MultipartFormData;
            }

            if (mediaType.StartsWith("image/"))
            {
                return HttpBodyContentKind.Image;
            }

            if (mediaType is "application/xml" or "text/xml")
            {
                return HttpBodyContentKind.Xml;
            }

            if (mediaType.EndsWith("+xml"))
            {
                return HttpBodyContentKind.Xml;
            }

            if (mediaType == "text/html")
            {
                return HttpBodyContentKind.Html;
            }

            if (mediaType.StartsWith("text/"))
            {
                return HttpBodyContentKind.Text;
            }

            return HttpBodyContentKind.Binary;
        }
    }
}
