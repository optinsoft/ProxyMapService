using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class TunnelHandler : IHandler
    {
        private static readonly TunnelHandler Self = new();
        private const int BufferSize = 8192;

        public async Task<HandleStep> Run(SessionContext context)
        {
            var clientStream = context.ClientStream;
            if (clientStream != null && !context.Token.IsCancellationRequested)
            {
                using var remoteStream = context.RemoteClient.GetStream();

                var headerBytes = context.TunnelHeaderBytes;

                if (headerBytes != null && headerBytes.Length > 0)
                {
                    await remoteStream.WriteAsync(headerBytes, context.Token);
                    context.SentCounter?.OnBytesSent(context, headerBytes.Length);
                }

                var forwardTask = Tunnel(clientStream, remoteStream, context, null, context.SentCounter);
                var reverseTask = Tunnel(remoteStream, clientStream, context, context.ReadCounter, null);

                await Task.WhenAny(forwardTask, reverseTask);
            }

            return HandleStep.Terminate;
        }

        private static async Task Tunnel(Stream source, Stream destination, SessionContext context, BytesReadCounter? readCounter, BytesSentCounter? sentCounter)
        {
            var buffer = new byte[BufferSize];

            CancellationToken token = context.Token;

            try
            {
                int bytesRead;
                do
                {
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    readCounter?.OnBytesRead(context, bytesRead);
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    sentCounter?.OnBytesSent(context, bytesRead);
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public static TunnelHandler Instance()
        {
            return Self;
        }
    }
}
