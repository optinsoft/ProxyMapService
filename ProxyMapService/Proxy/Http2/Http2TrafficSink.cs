namespace ProxyMapService.Proxy.Http2
{
    /// <summary>
    /// Observer event receiver. All methods are called synchronously, under the observer's shared lock.
    /// Do nothing by default: override what you need.
    /// </summary>
    public class Http2TrafficSink
    {
        public virtual void OnRequestHeaders(Http2StreamInfo stream) { }
        public virtual void OnRequestData(Http2StreamInfo stream, ReadOnlySpan<byte> data) { }
        public virtual void OnRequestTrailers(Http2StreamInfo stream) { }
        public virtual void OnRequestEnd(Http2StreamInfo stream) { }

        public virtual void OnResponseHeaders(Http2StreamInfo stream) { }
        public virtual void OnResponseData(Http2StreamInfo stream, ReadOnlySpan<byte> data) { }
        public virtual void OnResponseTrailers(Http2StreamInfo stream) { }
        public virtual void OnResponseEnd(Http2StreamInfo stream) { }

        public virtual void OnStreamReset(Http2StreamInfo stream, H2Direction by, uint errorCode) { }

        /// <summary>Stream completed (both ends, RST_STREAM, eviction, or connection close). Place to release resources.</summary>
        public virtual void OnStreamCompleted(Http2StreamInfo stream) { }

        public virtual void OnGoAway(H2Direction by, int lastStreamId, uint errorCode) { }

        /// <summary>Direction analysis stopped (parse error). Byte forwarding continues nonetheless.</summary>
        public virtual void OnObserverFailed(H2Direction direction, Exception exception) { }
    }
}
