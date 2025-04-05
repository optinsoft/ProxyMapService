using Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using System.Net.Sockets;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ProxyMapService.Proxy.Handlers
{
    public class ProxyHandler : IHandler
    {
        private static readonly ProxyHandler Self = new();
        private const int BufferSize = 8192;

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.mapping.Listen.RejectHttpProxy && context.Header?.Verb != "CONNECT")
            {
                await SendMethodNotAllowed(context);
                return HandleStep.Terminate;
            }

            string? proxyAuthorization = !context.mapping.Authentication.SetHeader 
                ? null 
                : Convert.ToBase64String(Encoding.ASCII.GetBytes($"{context.mapping.Authentication.Username}:{context.mapping.Authentication.Password}"));

            var headerBytes = context.Header?.GetBytes(proxyAuthorization);

            IPEndPoint remoteEndPoint = Address.GetIPEndPoint(context.mapping.ProxyServer.Host, context.mapping.ProxyServer.Port);

            using var remoteClient = new TcpClient();

            await remoteClient.ConnectAsync(remoteEndPoint, context.Token);

            if (!context.Token.IsCancellationRequested)
            {
                using var localStream = context.Client.GetStream();
                using var remoteStream = remoteClient.GetStream();

                if (headerBytes != null && headerBytes.Length > 0)
                {
                    await remoteStream.WriteAsync(headerBytes, context.Token);
                }

                //var forwardTask = localStream.CopyToAsync(remoteStream, context.Token);
                //var reverseTask = remoteStream.CopyToAsync(localStream, context.Token);

                var forwardTask = Tunnel(localStream, remoteStream, context);
                var reverseTask = Tunnel(remoteStream, localStream, context);

                await Task.WhenAny(forwardTask, reverseTask);
            }

            return HandleStep.Terminate;
        }

        private static async Task Tunnel(Stream source, Stream destination, SessionContext context)
        {
            var buffer = new byte[BufferSize];

            CancellationToken token = context.Token;

            try
            {
                int bytesRead;
                do
                {
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public static ProxyHandler Instance()
        {
            return Self;
        }

        private static async Task SendMethodNotAllowed(SessionContext context)
        {
            if (context.ClientStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 405 Method Not Allowed\r\nConnection: close\r\n\r\n");
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }
    }
}
