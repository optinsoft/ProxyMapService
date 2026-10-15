using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Counters
{
    public class HttpBodyLogger(bool response, IHttpLoggingSwitch? loggingSwitch) : IHttpBodyLogger
    {
        public void OnCompleted(object context, HttpRequestHeader? requestHeader, string? contentType, string? contentEncoding, long bodyLength, byte[] bodyBytes)
        {
            if (loggingSwitch?.IsHttpCapture == false) return;
            if (requestHeader == null) return;
            HttpBodyHandler?.Invoke(context, new()
            {
                HeaderId = requestHeader.RequestId,
                Response = response,
                Completed = true,
                ContentType = contentType,
                ContentEncoding = contentEncoding,
                RequestXMicrosoftAjax = requestHeader.XMicrosoftAjax,
                BodyLength = bodyLength,
                BodyBytes = bodyBytes
            });
        }

        public void OnCompleted(object context, HttpRequestHeader? requestHeader, string? contentType, long bodyLength, byte[] bodyBytes)
        {
            OnCompleted(context, requestHeader, contentType, null, bodyLength, bodyBytes);
        }

        public void OnCompleted(object context, HttpRequestHeader? requestHeader, string? contentType, string? contentEncoding, long bodyLength, MemoryStream? bodyStream)
        {
            if (loggingSwitch?.IsHttpCapture == false) return;
            if (requestHeader == null) return;
            if (HttpBodyHandler != null)
            {
                byte[]? bodyBytes = bodyStream?.ToArray();
                HttpBodyHandler.Invoke(context, new()
                {
                    HeaderId = requestHeader.RequestId,
                    Response = response,
                    Completed = true,
                    ContentType = contentType,
                    ContentEncoding = contentEncoding,
                    RequestXMicrosoftAjax= requestHeader.XMicrosoftAjax,
                    BodyLength = bodyLength,
                    BodyBytes = bodyBytes
                });
            }
        }

        public void OnCompleted(object context, HttpRequestHeader? requestHeader, string? contentType, long bodyLength, MemoryStream? bodyStream)
        {
            OnCompleted(context, requestHeader, contentType, null, bodyLength, bodyStream);
        }

        public event EventHandler<HttpBodyEventArgs>? HttpBodyHandler;
    }
}
