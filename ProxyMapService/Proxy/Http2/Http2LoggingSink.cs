using ProxyMapService.Proxy.Counters;
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
                    context.RequestHeaderHistory.TryRemove(st.Request.RequestNum, out _);
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

    /// <summary>Accumulates the body of a single message and, once complete, hands it to IHttpBodyLogger (like BodyTracker in h1).</summary>
    internal sealed class Http2BodyCollector(IHttpBodyLogger? bodyLogger, object context, HttpRequestHeader? requestHeader,
        string? contentType, string? contentEncoding) : IDisposable
    {
        private MemoryStream? _stream = bodyLogger != null ? new MemoryStream() : null;
        private long _length;
        private bool _completed;

        public void Append(ReadOnlySpan<byte> data)
        {
            if (_completed) return;
            _length += data.Length;
            _stream?.Write(data);
        }

        public void Complete()
        {
            if (_completed) return;
            _completed = true;
            // as in h1: a message without a body is not passed to the body logger
            if (_length > 0)
            {
                bodyLogger?.OnCompleted(context, requestHeader, contentType, contentEncoding, _length, _stream);
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    /// <summary>Builds regular h1 headers from h2 headers (for logging only; they are never sent over the wire).</summary>
    internal static class Http2HeaderBridge
    {
        public static HttpRequestHeader CreateRequestHeader(IReadOnlyList<H2Header> headers)
        {
            string method = "", path = "/";
            string? authority = null;
            bool hasHost = false;
            List<string>? cookies = null;
            var lines = new List<string>(headers.Count + 2) { "" }; // [0] — the start line; we'll fill it in at the end

            foreach (var (name, value) in headers)
            {
                switch (name)
                {
                    case ":method": method = Clean(value); continue;
                    case ":path": path = Clean(value); continue;
                    case ":authority": authority = Clean(value); continue;
                    case "cookie":
                        cookies ??= new List<string>();
                        cookies.Add(Clean(value)); // in h2, cookies may arrive in multiple header fields
                        continue;
                    case "host": hasHost = true; break;
                }
                if (name.StartsWith(':')) continue; // :scheme and other pseudo-headers
                lines.Add($"{Clean(name)}: {Clean(value)}");
            }

            if (cookies != null) lines.Add("cookie: " + string.Join("; ", cookies));
            if (authority != null && !hasHost) lines.Insert(1, "host: " + authority);
            lines[0] = $"{method} {path} HTTP/2";
            return new HttpRequestHeader(lines.ToArray());
        }

        public static HttpResponseHeader CreateResponseHeader(IReadOnlyList<H2Header> headers, int status)
        {
            var lines = new List<string>(headers.Count + 1) { "" };
            foreach (var (name, value) in headers)
            {
                if (name.StartsWith(':')) continue;
                lines.Add($"{Clean(name)}: {Clean(value)}");
            }
            lines[0] = $"HTTP/2 {status} {Http1Http2Converter.Reason(status)}";

            int length = 2; // trailing CRLF
            foreach (var l in lines) length += l.Length + 2;
            return new HttpResponseHeader(lines.ToArray(), length);
        }

        // protection of logs against line injection: CR/LF are not allowed in values in h2, but a peer may still send them
        private static string Clean(string s)
            => s.AsSpan().IndexOfAny('\r', '\n') < 0 ? s : s.Replace('\r', ' ').Replace('\n', ' ');
    }

    internal static partial class Http2Log
    {
        [LoggerMessage(EventId = 1501, Level = LogLevel.Debug,
            Message = "[h2 stream {StreamId}] {Method} {Path} -> {Status}, request {RequestBytes} B, response {ResponseBytes} B, {ElapsedMs} ms")]
        public static partial void StreamCompleted(ILogger logger, int streamId, string? method, string? path, int status,
            long requestBytes, long responseBytes, long elapsedMs);

        [LoggerMessage(EventId = 1502, Level = LogLevel.Debug,
            Message = "[h2 stream {StreamId}] reset by {By}, HTTP/2 error code {ErrorCode}")]
        public static partial void StreamReset(ILogger logger, int streamId, string by, uint errorCode);

        [LoggerMessage(EventId = 1503, Level = LogLevel.Debug,
            Message = "[h2] GOAWAY from {By}: last stream {LastStreamId}, HTTP/2 error code {ErrorCode}")]
        public static partial void GoAway(ILogger logger, string by, int lastStreamId, uint errorCode);

        [LoggerMessage(EventId = 1504, Level = LogLevel.Warning,
            Message = "[h2] traffic analysis stopped for {Direction}: {ExceptionName}: {ErrorMessage}. Forwarding continues, this direction is no longer logged.")]
        public static partial void ObserverFailed(ILogger logger, string direction, string exceptionName, string errorMessage);

        [LoggerMessage(EventId = 1505, Level = LogLevel.Warning,
            Message = "[h2 stream {StreamId}] cannot build an HTTP header for logging: {ErrorMessage}")]
        public static partial void CannotBuildHeader(ILogger logger, int streamId, string errorMessage);
    }
}
