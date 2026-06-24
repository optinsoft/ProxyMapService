
namespace ProxyMapService.Proxy.Counters
{
    public class HttpBodyLogger(bool response) : IHttpBodyLogger
    {
        public void OnData(MemoryStream? bodyStream, ReadOnlySpan<byte> data)
        {
            bodyStream?.Write(data);
        }

        public void OnCompleted(object context, string? contentType, long bodyLength, MemoryStream? bodyStream)
        {
            HttpBodyHandler?.Invoke(context, new()
            {
                Response = response,
                ContentType = contentType,
                BodyLength = bodyLength,
                BodyStream = bodyStream
            });
        }

        public event EventHandler<HttpBodyEventArgs>? HttpBodyHandler;
    }
}
