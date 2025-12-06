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
        private static int _tunnelId = 0;

        public async Task<HandleStep> Run(SessionContext context)
        {
            var clientStream = context.ClientStream;
            var remoteStream = context.RemoteStream;

            if (clientStream != null && remoteStream != null && !context.Token.IsCancellationRequested)
            {
                var forwardTask = Tunnel(clientStream, remoteStream, context, context.ClientReadCounter, context.RemoteSentCounter);
                var reverseTask = Tunnel(remoteStream, clientStream, context, context.RemoteReadCounter, context.ClientSentCounter);
                await Task.WhenAny(forwardTask, reverseTask);
            }

            return HandleStep.Terminate;
        }

        public static TunnelHandler Instance()
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
