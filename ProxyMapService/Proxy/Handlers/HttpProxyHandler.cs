using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;
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

            string? requestFirstLine = null;
            string? hostError = null;

            if (http.HTTPVerb == "CONNECT" && context.ProxyServer?.ResolveIP == true)
            {
                try
                {
                    IPEndPoint hostEndPoint = Address.GetIPEndPoint(context.HostName, context.HostPort);
                    requestFirstLine = $"{http?.HTTPVerb} {hostEndPoint.Address}:{hostEndPoint.Port} {http?.HTTPProtocol}";
                }
                catch (Exception ex)
                {
                    hostError = ex.Message;
                }
            }

            if (hostError == null)
            {
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
                    ? Convert.ToBase64String(Encoding.ASCII.GetBytes($"{GetContextProxyUsernameWithParameters(context)}:{context.ProxyServer.Password ?? String.Empty}"))
                    : (context.Mapping.Authentication.SetAuthentication
                    ? Convert.ToBase64String(Encoding.ASCII.GetBytes($"{GetContextAuthenticationUsernameWithParameters(context)}:{context.Mapping.Authentication.Password}"))
                    : (context.Mapping.Authentication.RemoveAuthentication ? "" : clientAuthorization));

                var httpRequestBytes = http?.GetBytes(true, proxyAuthorization, requestFirstLine);
                if (httpRequestBytes != null && httpRequestBytes.Length > 0)
                {
                    await SendHttpRequest(context, httpRequestBytes);

                    if (context.Http != null)
                    {
                        return HandleStep.Tunnel;
                    }

                    var responseHeaderBytes = context.OutgoingStream != null
                        ? await context.OutgoingHeaderStream.ReadHeaderBytes(context.OutgoingStream, context.Token)
                        : null;
                    if (responseHeaderBytes != null)
                    {
                        var responseHttp = new HttpResponseHeader(responseHeaderBytes);
                        if (responseHttp.StatusCode == "200")
                        {
                            if (context.Socks4 != null)
                            {
                                await Socks4Reply(context, Socks4Command.RequestGranted);
                            }
                            if (context.Socks5 != null)
                            {
                                await Socks5Reply(context, Socks5Status.Succeeded);
                            }
                            return HandleStep.Tunnel;
                        }
                    }
                }
            }

            if (context.Http != null)
            {
                await HttpReply(context, Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\n\r\n"));
            }
            if (context.Socks4 != null)
            {
                await Socks4Reply(context, Socks4Command.RequestRejectedOrFailed);
            }
            if (context.Socks5 != null)
            {
                await Socks5Reply(context, Socks5Status.GeneralFailure);
            }

            return HandleStep.Terminate;
        }

        public static HttpProxyHandler Instance()
        {
            return Self;
        }

        private static async Task SendHttpRequest(SessionContext context, byte[] requestBytes)
        {
            if (context.OutgoingStream == null) return;
            await context.OutgoingStream.WriteAsync(requestBytes, context.Token);
        }
    }
}
