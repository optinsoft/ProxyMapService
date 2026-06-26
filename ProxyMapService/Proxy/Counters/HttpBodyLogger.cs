
namespace ProxyMapService.Proxy.Counters
{
    public class HttpBodyLogger(bool response) : IHttpBodyLogger
    {
        public void OnCompleted(object context, string? contentType, long bodyLength, byte[] bodyBytes)
        {
            HttpBodyHandler?.Invoke(context, new()
            {
                Response = response,
                ContentType = contentType,
                BodyLength = bodyLength,
                BodyBytes = bodyBytes
            });
        }

        public void OnCompleted(object context, string? contentType, long bodyLength, MemoryStream? bodyStream)
        {
            if (HttpBodyHandler != null)
            {
                byte[]? bodyBytes = bodyStream?.ToArray();
                HttpBodyHandler.Invoke(context, new()
                {
                    Response = response,
                    ContentType = contentType,
                    BodyLength = bodyLength,
                    BodyBytes = bodyBytes
                });
            }
        }

        public event EventHandler<HttpBodyEventArgs>? HttpBodyHandler;
    }
}
