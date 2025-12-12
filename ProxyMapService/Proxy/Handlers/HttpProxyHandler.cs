using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Text;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpProxyHandler : BaseProxyHandler, IHandler
    {
        private static readonly HttpProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            var http = context.Http;

            if (http == null)
            {
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
                http = new HttpRequestHeader(connectBytes);
            }

            string? clientAuthorization =
                !String.IsNullOrEmpty(context.Socks5?.Username)
                ? Convert.ToBase64String(Encoding.ASCII.GetBytes($"{context.Socks5.Username}:{context.Socks5.Password ?? String.Empty}"))
                : (
                    !String.IsNullOrEmpty(context.Socks4?.UserId)
                    ? Convert.ToBase64String(Encoding.ASCII.GetBytes($"{context.Socks4.UserId}"))
                    : null
                    );

            string? proxyAuthorization = 
                !String.IsNullOrEmpty(context.ProxyServer?.Username)
                ? Convert.ToBase64String(Encoding.ASCII.GetBytes($"{context.ProxyServer.Username}:{context.ProxyServer.Password ?? String.Empty}"))
                : (context.Mapping.Authentication.SetAuthentication
                ? Convert.ToBase64String(Encoding.ASCII.GetBytes($"{context.Mapping.Authentication.Username}:{context.Mapping.Authentication.Password}"))
                : (context.Mapping.Authentication.RemoveAuthentication ? "" : clientAuthorization));

            var httpRequestBytes = http.GetBytes(true, proxyAuthorization, null);
            if (httpRequestBytes != null && httpRequestBytes.Length > 0)
            {
                await SendHttpRequest(context, httpRequestBytes);

                if (context.Http != null)
                {
                    return HandleStep.Tunnel;
                }

                var responseHeaderBytes = context.RemoteStream != null
                    ? await context.RemoteHeaderStream.ReadHeaderBytes(context.RemoteStream, context.Token)
                    : null;
                if (responseHeaderBytes != null)
                {
                    var responseHttp = new HttpResponseHeader(responseHeaderBytes);
                    if (responseHttp.StatusCode == "200")
                    {
                        if (context.Socks4 != null)
                        {
                            await SendSocks4Reply(context, Socks4Command.RequestGranted);
                        }
                        if (context.Socks5 != null)
                        {
                            await SendSocks5Reply(context, Socks5Status.Succeeded);
                        }
                        return HandleStep.Tunnel;
                    }
                }
            }

            if (context.Http != null)
            {
                await SendHttpReply(context, Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n"));
            }
            if (context.Socks4 != null)
            {
                await SendSocks4Reply(context, Socks4Command.RequestRejectedOrFailed);
            }
            if (context.Socks5 != null)
            {
                await SendSocks5Reply(context, Socks5Status.GeneralFailure);
            }

            return HandleStep.Terminate;
        }

        public static HttpProxyHandler Instance()
        {
            return Self;
        }

        private static async Task SendHttpRequest(SessionContext context, byte[] requestBytes)
        {
            if (context.RemoteStream == null) return;
            await context.RemoteStream.WriteAsync(requestBytes, context.Token);
        }
    }
}
