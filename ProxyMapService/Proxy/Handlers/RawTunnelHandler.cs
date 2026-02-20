using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
	public class RawTunnelHandler : IHandler
	{
        private static readonly RawTunnelHandler Self = new();
        private const int BufferSize = 8192;
        private static int _tunnelId = 0;

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && context.OutgoingStream != null && !context.Token.IsCancellationRequested)
            {
                var incomingStream = context.IncomingStream;
                var outgoingStream = context.OutgoingStream;

                var forwardTask = Tunnel(incomingStream, outgoingStream, context, context.IncomingReadCounter, context.OutgoingSentCounter);
                var reverseTask = Tunnel(outgoingStream, incomingStream, context, context.OutgoingReadCounter, context.IncomingSentCounter);
                await Task.WhenAny(forwardTask, reverseTask);
            }

            return HandleStep.Terminate;
        }

        public static RawTunnelHandler Instance()
        {
            return Self;
        }

        private static async Task Tunnel(Stream source, Stream destination, SessionContext context,
            IBytesReadCounter? readCounter, IBytesSentCounter? sentCounter)
        {
            var tunnelId = ++_tunnelId;

            var buffer = new byte[BufferSize];

            CancellationToken token = context.Token;

            try
            {
                int bytesRead;
                do
                {
                    if (readCounter != null)
                    {
                        context.Logger.LogDebug("Tunnel {tunnelId}: reading from {direction}...", tunnelId, readCounter.Direction);
                    }
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    if (bytesRead > 0)
                    {
                        if (sentCounter != null)
                        {
                            context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...", tunnelId, sentCounter.Direction);
                        }
                        await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    }
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException)
            {
                //context.Logger.LogError("ObjectDisposedException: {ErrorMessage}", ex.Message);
            }
        }
    }
}
