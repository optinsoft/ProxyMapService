using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Tunnels
{
    public class H1ToH2RequestTunnel : HttpRequestTunnel
    {
        private readonly Http2ClientConnection _conn;
        private int _streamId;

        public H1ToH2RequestTunnel(
            SessionContext context,
            CountingStream source,
            CountingStream destination,
            TaskCompletionSource destinationReady,
            BytesReadCounter readCounter,
            BytesSendCounter sendCounter,
            TunnelState selfState,
            TunnelState otherTunnelState,
            Http2ClientConnection conn)
            : base(
            context,
            source,
            destination,
            destinationReady,
            readCounter,
            sendCounter,
            selfState,
            otherTunnelState)
        {
            _conn = conn;
        }

        protected override async Task SendHeaderAsync(string[] lines, IHttpBodyTracker tracker, CancellationToken token)
        {
            var h = new HttpRequestHeader(lines);
            if (!Http1Http2Converter.TryBuildRequest(h, true, out var headers, out var err))
            {
                await HttpProto.HttpReplyError(_context, h, _source, "HTTP/1.1 501 Not Implemented", err);
                throw new IOException(err);
            }
            _streamId = await _conn.BeginRequestAsync(h.HTTPVerb!, h.KeepAlive, headers, endStream: tracker.Completed, token);
        }

        protected override async Task SendBodyAsync(ReadOnlyMemory<byte> bytes, IHttpBodyTracker tracker, CancellationToken token)
        {
            var ranges = tracker.Payload;
            bool done = tracker.Completed;
            var trailers = done ? tracker.Trailers : null;
            bool hasTrailers = trailers is { Count: > 0 };
            for (int i = 0; i < ranges.Count; i++)
                await _conn.SendDataAsync(_streamId, bytes.Slice(ranges[i].Offset, ranges[i].Length),
                    done && !hasTrailers && i == ranges.Count - 1, token);
            if (done)
            {
                if (hasTrailers) await _conn.SendTrailersAsync(_streamId, Http1Http2Converter.BuildTrailers(trailers!), token);
                else if (ranges.Count == 0) await _conn.SendDataAsync(_streamId, default, true, token);
                _conn.MarkRequestEnded();
            }
        }
    }
}
