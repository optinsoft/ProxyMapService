using ProxyMapService.Proxy.Network;
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
            using var outgoingClient = new TcpClient();

            context.ProxyServer ??= context.ProxyProvider.GetProxyServer(context);

            IPEndPoint outgoingEndPoint = HostAddress.GetIPEndPoint(context.ProxyServer.Host, context.ProxyServer.Port);

            await outgoingClient.ConnectAsync(outgoingEndPoint, context.Token);

            using var incomingStream = context.IncomingClient.GetStream();
            using var outgoingStream = outgoingClient.GetStream();

            // Forward both directions simultaneously
            var forwardTask = incomingStream.CopyToAsync(outgoingStream, context.Token);
            var reverseTask = outgoingStream.CopyToAsync(incomingStream, context.Token);

            await Task.WhenAny(forwardTask, reverseTask);

            return HandleStep.Terminate;
        }

        public static ForwardHandler Instance()
        {
            return Self;
        }
    }
}
