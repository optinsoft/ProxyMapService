using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class TunnelHandler : IHandler
    {
        private static readonly TunnelHandler Self = new();
        private const int BufferSize = 8192;

        public async Task<HandleStep> Run(SessionContext context)
        {
            var clientStream = context.ClientStream;
            var remoteStream = context.RemoteStream;

            if (clientStream != null && remoteStream != null && !context.Token.IsCancellationRequested)
            {
                var forwardTask = Tunnel(clientStream, remoteStream, context, null, context.SentCounter);
                var reverseTask = Tunnel(remoteStream, clientStream, context, context.ReadCounter, null);
                await Task.WhenAny(forwardTask, reverseTask);
            }

            return HandleStep.Terminate;
        }

        public static TunnelHandler Instance()
        {
            return Self;
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
                    readCounter?.OnBytesRead(context, bytesRead, buffer, 0);
                    if (bytesRead > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        sentCounter?.OnBytesSent(context, bytesRead, buffer, 0);
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
