using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Utils
{
    public static class HttpBodyUtils
    {
        public static IHttpBodyTracker CreateRequestBodyTracker(SessionContext context, 
            HttpRequestHeader requestHeader, MemoryStream? accumulateBodyStream)
        {
            if (context.RequestBodyTracker != null)
            {
                context.Logger.LogNotNullRequestBodyTracker(context.Host);
                //Debug.Assert(false, "Context RequestBodyTracker is not null");
            }
            IHttpBodyTracker tracker;
            if (requestHeader.TransferEncodingChunked)
            {
                tracker = new HttpChunkedBodyTracker(
                    context.Logger,
                    false,
                    requestHeader,
                    requestHeader.ContentType,
                    requestHeader.ContentEncoding,
                    RequestHasBody(requestHeader),
                    context.RequestBodyLogger,
                    context,
                    context.RequestBodyLogger != null || accumulateBodyStream != null,
                    accumulateBodyStream);
            }
            else
            {
                tracker = new HttpBodyTracker(
                    context.Logger,
                    false,
                    requestHeader,
                    requestHeader.ContentType,
                    requestHeader.ContentEncoding,
                    requestHeader.ContentLength ?? 0,
                    RequestHasBody(requestHeader),
                    context.RequestBodyLogger,
                    context,
                    context.RequestBodyLogger != null || accumulateBodyStream != null,
                    accumulateBodyStream);
            }
            context.RequestBodyTracker = tracker;
            return tracker;
        }

        public static IHttpBodyTracker CreateResponseBodyTracker(SessionContext context, 
            HttpResponseHeader responseHeader, HttpRequestHeader? requestHeader)
        {
            if (context.ResponseBodyTracker != null)
            {
                context.Logger.LogNotNullResponseBodyTracker(context.Host);
                //Debug.Assert(false, "Context ResponseBodyTracker is not null");
            }
            IHttpBodyTracker tracker;
            if (responseHeader.TransferEncodingChunked)
            {
                tracker = new HttpChunkedBodyTracker(
                    context.Logger,
                    true,
                    requestHeader,
                    responseHeader.ContentType,
                    responseHeader.ContentEncoding,
                    ResponseHasBody(responseHeader, requestHeader?.HTTPVerb),
                    context.ResponseBodyLogger, 
                    context,
                    context.ResponseBodyLogger != null,
                    null);
            }
            else
            {
                tracker = new HttpBodyTracker(
                    context.Logger,
                    true,
                    requestHeader,
                    responseHeader.ContentType,
                    responseHeader.ContentEncoding,
                    responseHeader.ContentLength ?? 0,
                    ResponseHasBody(responseHeader, requestHeader?.HTTPVerb),
                    context.ResponseBodyLogger,
                    context,
                    context.ResponseBodyLogger != null,
                    null);
            }
            context.ResponseBodyTracker = tracker;
            return tracker;
        }

        public static bool RequestHasBody(HttpRequestHeader requestHeader)
        {
            switch (requestHeader.HTTPVerb)
            {
                case "GET":
                    return false;
                case "DELETE":
                    return false;
                case "HEAD":
                    return false;
                case "OPTIONS":
                    return false;
                case "TRACE":
                    return false;
                case "CONNECT":
                    return false;
            };
            return (requestHeader.TransferEncoding != null) 
                || (requestHeader.ContentLength.HasValue && requestHeader.ContentLength.Value > 0);
        }

        public static bool ResponseHasBody(HttpResponseHeader responseHeader, string? requestMethod)
        {
            if (requestMethod == "HEAD")
            { 
                return false;
            }
            if (responseHeader.StatusCode?.Length == 3)
            {
                if (responseHeader.StatusCode.StartsWith('1')
                    || responseHeader.StatusCode == "204"
                    || responseHeader.StatusCode == "304")
                {
                    return false;
                }
            }
            return (responseHeader.TransferEncoding != null) 
                || (responseHeader.ContentLength.HasValue && responseHeader.ContentLength.Value > 0);
        }
    }
}
