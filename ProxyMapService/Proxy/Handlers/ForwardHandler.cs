using Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using System.Net;
using System.Net.Sockets;

namespace ProxyMapService.Proxy.Handlers
{
    public class ForwardHandler : IHandler
    {
        private static readonly ForwardHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            using var remoteClient = new TcpClient();

            IPEndPoint remoteEndPoint = Address.GetIPEndPoint(context.Mapping.ProxyServer.Host, context.Mapping.ProxyServer.Port);

            await remoteClient.ConnectAsync(remoteEndPoint, context.Token);

            using var localStream = context.Client.GetStream();
            using var remoteStream = remoteClient.GetStream();

            // Forward both directions simultaneously
            var forwardTask = localStream.CopyToAsync(remoteStream, context.Token);
            var reverseTask = remoteStream.CopyToAsync(localStream, context.Token);

            await Task.WhenAny(forwardTask, reverseTask);

            return HandleStep.Terminate;
        }

        public static ForwardHandler Instance()
        {
            return Self;
        }
    }
}
