using ProxyMapService.Proxy.Sessions;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Http2
{
    /// <summary>
    /// Attaches a passive HTTP/2 observer to the existing HTTP/1.1 logging:
    /// regular HttpRequestHeader / HttpResponseHeader objects are assembled from the h2 headers and passed to the same
    /// RequestHeadersLogger / ResponseHeadersLogger / Request(Response)BodyLogger as in h1 tunnels.
    /// </summary>
    public sealed class Http2LoggingSink(SessionContext context) : Http2TrafficSink
    {
        private sealed class StreamLog : IDisposable
        {
            public HttpRequestHeader? Request;
            public Http2BodyCollector? RequestBody;
            public Http2BodyCollector? ResponseBody;

            public void Dispose()
            {
                RequestBody?.Dispose();
                ResponseBody?.Dispose();
            }
        }

        private static StreamLog State(Http2StreamInfo s)
        {
            if (s.Tag is not StreamLog st)
            {
                st = new StreamLog();
                s.Tag = st;
            }
            return st;
        }

        public override void OnRequestHeaders(Http2StreamInfo stream)
        {
            HttpRequestHeader request;
            try
            {
                request = Http2HeaderBridge.CreateRequestHeader(stream.RequestHeaders!);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
            {
                Http2Log.CannotBuildHeader(context.Logger, stream.StreamId, ex.Message);
                return;
            }

            State(stream).Request = request;
            context.RequestHeader = request; // assigns RequestNum and puts the header into RequestHeaderHistory
            context.RequestHeadersLogger?.OnHttpRequestHeader(context, request);
            if (!request.BadRequest)
            {
                context.SessionManager.NotifyIfRequestUrlMatches(context, request, context.RequestTunnelState.IsSecure);
            }
        }

        public override void OnRequestData(Http2StreamInfo stream, ReadOnlySpan<byte> data)
        {
            var st = State(stream);
            st.RequestBody ??= new Http2BodyCollector(context.RequestBodyLogger, context, st.Request,
                st.Request?.ContentType, st.Request?.ContentEncoding);
            st.RequestBody.Append(data);
        }

        public override void OnRequestEnd(Http2StreamInfo stream)
        {
            (stream.Tag as StreamLog)?.RequestBody?.Complete();
        }

        public override void OnResponseHeaders(Http2StreamInfo stream)
        {
            var st = State(stream);
            HttpResponseHeader response;
            try
            {
                response = Http2HeaderBridge.CreateResponseHeader(stream.ResponseHeaders!, stream.Status);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
            {
                Http2Log.CannotBuildHeader(context.Logger, stream.StreamId, ex.Message);
                return;
            }

            context.ResponseHeader = response; // assigns the next ResponseNum...
            if (st.Request != null)
            {
                // ...but in h2 responses don't arrive in order: associate it with the request of the same stream
                response.ResponseNum = st.Request.RequestNum;
                response.ResponseId = st.Request.RequestId;
            }
            context.ResponseHeadersLogger?.OnHttpResponseHeader(context, response, st.Request);

            st.ResponseBody = new Http2BodyCollector(context.ResponseBodyLogger, context, st.Request,
                response.ContentType, response.ContentEncoding);
        }

        public override void OnResponseData(Http2StreamInfo stream, ReadOnlySpan<byte> data)
        {
            (stream.Tag as StreamLog)?.ResponseBody?.Append(data);
        }

        public override void OnResponseEnd(Http2StreamInfo stream)
        {
            if (stream.Tag is StreamLog st)
            {
                st.ResponseBody?.Complete();
                context.CompleteRequest(st.Request); // completion of this specific request, not the last one in the session
            }
        }

        public override void OnStreamReset(Http2StreamInfo stream, H2Direction by, uint errorCode)
        {
            Http2Log.StreamReset(context.Logger, stream.StreamId, by.ToString(), errorCode);
        }

        public override void OnStreamCompleted(Http2StreamInfo stream)
        {
            Http2Log.StreamCompleted(context.Logger, stream.StreamId, stream.Method, stream.Path, stream.Status,
                stream.RequestBytes, stream.ResponseBytes, (long)(DateTime.UtcNow - stream.StartedUtc).TotalMilliseconds);
            if (stream.Tag is StreamLog st)
            {
                // in h2 there are very many streams per connection: we clean up the request history when a stream completes
                if (st.Request != null)
                {
                    // the stream may have ended without a response (RST_STREAM, disconnect): mark it as completed, retrying is harmless
                    context.CompleteRequest(st.Request);
                }
                st.Dispose();
                stream.Tag = null;
            }
        }

        public override void OnGoAway(H2Direction by, int lastStreamId, uint errorCode)
        {
            Http2Log.GoAway(context.Logger, by.ToString(), lastStreamId, errorCode);
        }

        public override void OnObserverFailed(H2Direction direction, Exception exception)
        {
            Http2Log.ObserverFailed(context.Logger, direction.ToString(), exception.GetType().Name, exception.Message);
        }
    }
}
