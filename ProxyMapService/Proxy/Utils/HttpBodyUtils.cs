using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Utils
{
    public static class HttpBodyUtils
    {
        public static void CreateRequestBodyTracker(SessionContext context, byte[]? bodyBytes)
        {
            if (context.RequestHeader != null)
            {
                if (context.RequestHeader.TransferEncodingChunked)
                {
                    context.RequestBodyTracker = new ChunkedBodyTracker(
                        context.Logger,
                        context.RequestHeader.ContentType,
                        context.RequestHeader.ContentEncoding,
                        context.RequestBodyLogger,
                        context,
                        context.RequestBodyLogger != null);
                }
                else
                {
                    context.RequestBodyTracker = new BodyTracker(
                        context.Logger,
                        context.RequestHeader.ContentType,
                        context.RequestHeader.ContentEncoding,
                        context.RequestHeader.ContentLength ?? 0,
                        context.RequestBodyLogger,
                        context,
                        context.RequestBodyLogger != null);
                }
                if (bodyBytes != null)
                {
                    context.RequestBodyTracker.TryAppend(bodyBytes);
                }
            }
        }

        public static void CreateResponseBodyTracker(SessionContext context, byte[]? bodyBytes)
        {
            if (context.ResponseHeader != null)
            {
                if (context.ResponseHeader.TransferEncodingChunked)
                {
                    context.ResponseBodyTracker = new ChunkedBodyTracker(
                        context.Logger,
                        context.ResponseHeader.ContentType,
                        context.ResponseHeader.ContentEncoding,
                        context.ResponseBodyLogger,
                        context,
                        context.ResponseBodyLogger != null);
                }
                else
                {
                    context.ResponseBodyTracker = new BodyTracker(
                        context.Logger,
                        context.ResponseHeader.ContentType,
                        context.ResponseHeader.ContentEncoding,
                        context.ResponseHeader.ContentLength ?? 0,
                        context.ResponseBodyLogger,
                        context,
                        context.ResponseBodyLogger != null);
                }
                if (bodyBytes != null)
                {
                    context.ResponseBodyTracker.TryAppend(bodyBytes);
                }
            }
        }        
    }
}
