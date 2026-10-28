using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Tunnels
{
    /// <summary>
    /// h2 → h2: client bytes are sent to the server unchanged, while a copy of them is parsed by a passive observer.
    /// </summary>
    public class Http2RequestTunnel : HttpTunnel
    {
        private readonly Http2TrafficObserver _observer;

        public Http2RequestTunnel(
            SessionContext context,
            CountingStream source,
            CountingStream destination,
            TaskCompletionSource destinationReady,
            BytesReadCounter readCounter,
            BytesSendCounter sendCounter,
            TunnelState selfState,
            TunnelState otherTunnelState,
            Http2TrafficObserver observer)
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
            _observer = observer;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _observer.Close();
            base.Dispose(disposing);
        }

        protected override async Task ProcessAsync(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            // forwarding first: analysis must not add latency and cannot break it
            await SendRawAsync(data, token);
            _observer.Feed(H2Direction.ClientToServer, data.Span);
        }
    }
}
