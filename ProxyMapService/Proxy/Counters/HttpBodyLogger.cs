
namespace ProxyMapService.Proxy.Counters
{
    public class HttpBodyLogger(bool response) : IHttpBodyLogger
    {
        public void OnCompleted(object context, string? contentType, string? contentEncoding, long bodyLength, byte[] bodyBytes)
        {
            HttpBodyHandler?.Invoke(context, new()
            {
                Response = response,
                Completed = true,
                ContentType = contentType,
                ContentEncoding = contentEncoding,
                BodyLength = bodyLength,
                BodyBytes = bodyBytes
            });
        }

        public void OnCompleted(object context, string? contentType, long bodyLength, byte[] bodyBytes)
        {
            OnCompleted(context, contentType, null, bodyLength, bodyBytes);
        }

        public void OnCompleted(object context, string? contentType, string? contentEncoding, long bodyLength, MemoryStream? bodyStream)
        {
            if (HttpBodyHandler != null)
            {
                byte[]? bodyBytes = bodyStream?.ToArray();
                HttpBodyHandler.Invoke(context, new()
                {
                    Response = response,
                    Completed = true,
                    ContentType = contentType,
                    ContentEncoding = contentEncoding,
                    BodyLength = bodyLength,
                    BodyBytes = bodyBytes
                });
            }
        }

        public void OnCompleted(object context, string? contentType, long bodyLength, MemoryStream? bodyStream)
        {
            OnCompleted(context, contentType, null, bodyLength, bodyStream);
        }


        public event EventHandler<HttpBodyEventArgs>? HttpBodyHandler;
    }
}
