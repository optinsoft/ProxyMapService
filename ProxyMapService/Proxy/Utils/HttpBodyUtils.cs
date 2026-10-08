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
        public static void CreateRequestBodyTracker(SessionContext context, HttpRequestHeader? requestHeader, 
            byte[]? bodyBytes, MemoryStream? accumulateBodyStream)
        {
            if (requestHeader != null)
            {
                if (context.RequestBodyTracker != null)
                {
                    context.Logger.LogNotNullRequestBodyTracker(context.Host);
                    //Debug.Assert(false, "Context RequestBodyTracker is not null");
                }
                if (requestHeader.TransferEncodingChunked)
                {
                    context.RequestBodyTracker = new ChunkedBodyTracker(
                        context.Logger,
                        requestHeader.ContentType,
                        requestHeader.ContentEncoding,
                        context.RequestBodyLogger,
                        context,
                        context.RequestBodyLogger != null || accumulateBodyStream != null,
                        accumulateBodyStream);
                }
                else
                {
                    context.RequestBodyTracker = new BodyTracker(
                        context.Logger,
                        requestHeader.ContentType,
                        requestHeader.ContentEncoding,
                        requestHeader.ContentLength ?? 0,
                        context.RequestBodyLogger,
                        context,
                        context.RequestBodyLogger != null || accumulateBodyStream != null,
                        accumulateBodyStream);
                }
                if (bodyBytes != null)
                {
                    context.RequestBodyTracker.TryAppend(bodyBytes);
                }
            }
        }

        public static void CreateResponseBodyTracker(SessionContext context, HttpResponseHeader? responseHeader, 
            byte[]? bodyBytes, MemoryStream? accumulateBodyStream)
        {
            if (responseHeader != null)
            {
                if (context.ResponseBodyTracker != null)
                {
                    context.Logger.LogNotNullResponseBodyTracker(context.Host);
                    //Debug.Assert(false, "Context ResponseBodyTracker is not null");
                }
                if (responseHeader.TransferEncodingChunked)
                {
                    context.ResponseBodyTracker = new ChunkedBodyTracker(
                        context.Logger,
                        responseHeader.ContentType,
                        responseHeader.ContentEncoding,
                        context.ResponseBodyLogger,
                        context,
                        context.ResponseBodyLogger != null || accumulateBodyStream != null,
                        accumulateBodyStream);
                }
                else
                {
                    context.ResponseBodyTracker = new BodyTracker(
                        context.Logger,
                        responseHeader.ContentType,
                        responseHeader.ContentEncoding,
                        responseHeader.ContentLength ?? 0,
                        context.ResponseBodyLogger,
                        context,
                        context.ResponseBodyLogger != null || accumulateBodyStream != null,
                        accumulateBodyStream);
                }
                if (bodyBytes != null)
                {
                    context.ResponseBodyTracker.TryAppend(bodyBytes);
                }
            }
        }
    }
}
