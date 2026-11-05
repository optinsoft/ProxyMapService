using System.Buffers;
using System.Buffers.Binary;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Tunnels
{
    /// <summary>
    /// h2 → h2 with header rewriting (e.g. host override): every client header block is decoded, rewritten and
    /// re-encoded before reaching the server; all other frames are passed through unchanged.
    /// The observer keeps receiving the client's original bytes, so its HPACK state and the logs stay the same
    /// as in the plain passthrough; the rewriting itself is logged via the rewriter's onRewritten callback.
    /// </summary>
    public partial class Http2RequestRewriteTunnel : HttpTunnel
    {
        private readonly Http2TrafficObserver _observer;
        private readonly Http2HeaderRewriter _rewriter;
        private bool _failed;

        public Http2RequestRewriteTunnel(
            SessionContext context,
            CountingStream source,
            CountingStream destination,
            TaskCompletionSource destinationReady,
            BytesReadCounter readCounter,
            BytesSendCounter sendCounter,
            TunnelState selfState,
            TunnelState otherTunnelState,
            Http2TrafficObserver observer,
            Http2HeaderRewriter rewriter)
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
            _rewriter = rewriter;
        }

        protected override async Task ProcessAsync(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            if (_failed) return;

            var output = new ArrayBufferWriter<byte>(data.Length + 64);
            try
            {
                _rewriter.Transform(data.Span, output);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRewriteFailed(_context.Logger, _selfState.TunnelId, ex.GetType().Name, ex.Message);
                _failed = true;
                // complete frames emitted before the violation are valid, the rest is withheld
                if (output.WrittenCount > 0)
                {
                    await SendRawAsync(output.WrittenMemory, token);
                }
                await SendGoAwayAsync(token);
                return;
            }

            if (output.WrittenCount > 0)
            {
                await SendRawAsync(output.WrittenMemory, token);
            }
            // logging follows the client's original encoding: the observer's HPACK state must track the client's
            _observer.Feed(H2Direction.ClientToServer, data.Span);
        }

        private async Task SendGoAwayAsync(CancellationToken token)
        {
            // the header block could not be re-encoded: a compression error for the whole connection
            var goaway = new byte[13];
            goaway[0] = 4; // payload length
            goaway[3] = H2.GoAway;
            BinaryPrimitives.WriteUInt32BigEndian(goaway.AsSpan(9, 4), H2Error.CompressionError);
            await SendRawAsync(goaway, token);
        }

        #region High-Performance Logging

        [LoggerMessage(
            EventId = 1410,
            Level = LogLevel.Error,
            Message = "[Tunnel {TunnelId}] HTTP/2 header rewrite failed: {ExceptionName}: {ErrorMessage}. GOAWAY is sent to the client")]
        private static partial void LogRewriteFailed(ILogger logger, long tunnelId, string exceptionName, string errorMessage);

        #endregion
    }
}
