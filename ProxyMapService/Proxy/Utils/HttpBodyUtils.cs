using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using System.Diagnostics;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Utils
{
    public static class HttpBodyUtils
    {
        public static IBodyTracker CreateRequestBodyTracker(SessionContext context, HttpRequestHeader requestHeader, 
            MemoryStream? accumulateBodyStream)
        {
            if (context.RequestBodyTracker != null)
            {
                context.Logger.LogNotNullRequestBodyTracker(context.Host);
                //Debug.Assert(false, "Context RequestBodyTracker is not null");
            }
            IBodyTracker tracker;
            if (requestHeader.TransferEncodingChunked)
            {
                tracker = new ChunkedBodyTracker(
                    context.Logger,
                    false,
                    requestHeader.ContentType,
                    requestHeader.ContentEncoding,
                    context.RequestBodyLogger,
                    context,
                    context.RequestBodyLogger != null || accumulateBodyStream != null,
                    accumulateBodyStream);
            }
            else
            {
                tracker = new BodyTracker(
                    context.Logger,
                    false,
                    requestHeader.ContentType,
                    requestHeader.ContentEncoding,
                    requestHeader.ContentLength ?? 0,
                    context.RequestBodyLogger,
                    context,
                    context.RequestBodyLogger != null || accumulateBodyStream != null,
                    accumulateBodyStream);
            }
            return tracker;
        }

        public static IBodyTracker CreateResponseBodyTracker(SessionContext context, HttpResponseHeader responseHeader, 
            MemoryStream? accumulateBodyStream)
        {
            if (context.ResponseBodyTracker != null)
            {
                context.Logger.LogNotNullResponseBodyTracker(context.Host);
                //Debug.Assert(false, "Context ResponseBodyTracker is not null");
            }
            IBodyTracker tracker;
            if (responseHeader.TransferEncodingChunked)
            {
                tracker = new ChunkedBodyTracker(
                    context.Logger,
                    true,
                    responseHeader.ContentType,
                    responseHeader.ContentEncoding,
                    context.ResponseBodyLogger,
                    context,
                    context.ResponseBodyLogger != null || accumulateBodyStream != null,
                    accumulateBodyStream);
            }
            else
            {
                tracker = new BodyTracker(
                    context.Logger,
                    true,
                    responseHeader.ContentType,
                    responseHeader.ContentEncoding,
                    responseHeader.ContentLength ?? 0,
                    context.ResponseBodyLogger,
                    context,
                    context.ResponseBodyLogger != null || accumulateBodyStream != null,
                    accumulateBodyStream);
            }
            return tracker;
        }
    }
}
