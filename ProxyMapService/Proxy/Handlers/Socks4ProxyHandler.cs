using Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Text;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4ProxyHandler : IHandler
    {
        private static readonly Socks4ProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Proxified = true;

            context.SessionsCounter?.OnHostProxified(context);

            IPEndPoint remoteEndPoint = Address.GetIPEndPoint(context.Mapping.ProxyServer.Host, context.Mapping.ProxyServer.Port);

            try
            {
                await context.RemoteClient.ConnectAsync(remoteEndPoint, context.Token);
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnProxyFailed(context);
                await SendSocks4Reply(context, Socks4Command.RequestRejectedOrFailed);
                throw;
            }

            context.SessionsCounter?.OnProxyConnected(context);

            context.RemoteStream = context.RemoteClient.GetStream();

            string? userAgentHeader = !string.IsNullOrEmpty(context.UserAgent) ? $"User-Agent: {context.UserAgent}" : null;

            List<string> connectHttpCommand = [
                $"CONNECT {context.HostName}:{context.HostPort} HTTP/1.1",
                $"Host: {context.HostName}:{context.HostPort}"

            ];
            if (!string.IsNullOrEmpty(context.UserAgent))
            {
                connectHttpCommand.Add($"User-Agent: {context.UserAgent}");
            }
            connectHttpCommand.Add("Proxy-Connection: Keep-Alive");
            var connectBytes = Encoding.ASCII.GetBytes(String.Join("\r\n", [.. connectHttpCommand]));
            context.Http = new HttpRequestHeader(connectBytes);

            string? socks5ProxyAuthorization = context.Socks5?.Username != null && context.Socks5?.Username.Length > 0
                ? Convert.ToBase64String(Encoding.ASCII.GetBytes($"{context.Socks5.Username}:{context.Socks5.Password ?? String.Empty}"))
                : null;

            string? proxyAuthorization = !context.Mapping.Authentication.SetAuthentication
                ? socks5ProxyAuthorization
                : Convert.ToBase64String(Encoding.ASCII.GetBytes($"{context.Mapping.Authentication.Username}:{context.Mapping.Authentication.Password}"));

            var headerBytes = context.Http.GetBytes(true, proxyAuthorization, null);
            if (headerBytes != null && headerBytes.Length > 0)
            {
                await SendHttpHeaderBytes(context, headerBytes);
            }

            var responseHeaderBytes = await context.RemoteHeaderStream.ReadHeaderBytes(context.RemoteStream, context.Token);
            if (responseHeaderBytes != null)
            {
                var responseHttp = new HttpResponseHeader(responseHeaderBytes);
                if (responseHttp.StatusCode == "200")
                {
                    await SendSocks4Reply(context, Socks4Command.RequestGranted);
                    return HandleStep.Tunnel;
                }
            }

            await SendSocks4Reply(context, Socks4Command.RequestRejectedOrFailed);
            return HandleStep.Terminate;
        }

        public static Socks4ProxyHandler Instance()
        {
            return Self;
        }

        private static async Task SendSocks4Reply(SessionContext context, Socks4Command command)
        {
            if (context.ClientStream == null) return;
            byte[] bytes = [0x0, (byte)command, 0, 0, 0, 0, 0, 0];
            if (context.Socks4 != null)
            {
                Array.Copy(context.Socks4.Bytes, 2, bytes, 2, 6);
            }
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }

        private static async Task SendHttpHeaderBytes(SessionContext context, byte[] headerBytes)
        {
            if (context.RemoteStream == null) return;
            await context.RemoteStream.WriteAsync(headerBytes, context.Token);
            context.SentCounter?.OnBytesSent(context, headerBytes.Length, headerBytes, 0);
        }
    }
}
