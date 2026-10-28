using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Tunnels
{
    /// <summary>
    /// h2 → h2: server bytes are sent to the client unchanged, while a copy of them is parsed by a passive observer.
    /// </summary>
    public class Http2ResponseTunnel : HttpTunnel
    {
        private readonly Http2TrafficObserver _observer;

        public Http2ResponseTunnel(
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
            _observer.Feed(H2Direction.ServerToClient, data.Span);
        }
    }
}
