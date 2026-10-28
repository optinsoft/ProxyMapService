namespace ProxyMapService.Proxy.Http2
{
    /// <summary>State of a single h2 stream as seen by a passive observer.</summary>
    public sealed class Http2StreamInfo
    {
        internal Http2StreamInfo(int streamId, bool pushed)
        {
            StreamId = streamId;
            Pushed = pushed;
            StartedUtc = DateTime.UtcNow;
        }

        public int StreamId { get; }
        public bool Pushed { get; }
        public DateTime StartedUtc { get; }

        public IReadOnlyList<H2Header>? RequestHeaders { get; internal set; }
        public IReadOnlyList<H2Header>? RequestTrailers { get; internal set; }
        public IReadOnlyList<H2Header>? ResponseHeaders { get; internal set; }
        public IReadOnlyList<H2Header>? ResponseTrailers { get; internal set; }

        /// <summary>Final response status code (>= 200); 0 while there is no response yet.</summary>
        public int Status { get; internal set; }
        public long RequestBytes { get; internal set; }
        public long ResponseBytes { get; internal set; }
        public bool RequestEnded { get; internal set; }
        public bool ResponseEnded { get; internal set; }
        public bool Reset { get; internal set; }

        /// <summary>Place for the event sink's state.</summary>
        public object? Tag { get; set; }

        public string? Method => Find(RequestHeaders, ":method");
        public string? Path => Find(RequestHeaders, ":path");
        public string? Authority => Find(RequestHeaders, ":authority");

        public static string? Find(IReadOnlyList<H2Header>? headers, string name)
        {
            if (headers == null) return null;
            foreach (var h in headers)
                if (h.Name == name) return h.Value;
            return null;
        }
    }
}
