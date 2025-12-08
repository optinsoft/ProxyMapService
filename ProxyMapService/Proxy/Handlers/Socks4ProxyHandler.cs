using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4ProxyHandler : BaseProxyHandler, IHandler
    {
        private static readonly Socks4ProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            var socks4 = context.Socks4;

            if (socks4 == null)
            {
                var httpProxyAuthorization =
                    !String.IsNullOrEmpty(context.Http?.ProxyAuthorization)
                    ? Encoding.ASCII.GetString(Convert.FromBase64String(context.Http.ProxyAuthorization)).Split(':')
                    : null;
                string? clientUserid =
                    httpProxyAuthorization != null
                    ? httpProxyAuthorization[0]
                    : (!String.IsNullOrEmpty(context.Socks5?.Username) ? context.Socks5?.Username : context.Socks4?.UserId);
                var connectBytes = Socks4Header.GetConnectRequestBytes(context.HostName, context.HostPort, clientUserid);
                socks4 = new Socks4Header(connectBytes);
            }

            context.RemoteHeaderStream.SocksVersion = 0x04;

            string? userId = 
                !String.IsNullOrEmpty(context.ProxyServer?.Username) 
                ? context.ProxyServer?.Username
                : context.Mapping.Authentication.SetAuthentication ? context.Mapping.Authentication.Username : socks4.UserId;

            var socks4ConnectBytes = Socks4Header.GetConnectRequestBytes(context.HostName, context.HostPort, userId);
            await SendSocks4Request(context, socks4ConnectBytes);

            if (context.Socks4 != null)
            {
                return HandleStep.Tunnel;
            }

            var responseHeaderBytes = await context.RemoteHeaderStream.ReadHeaderBytes(context.RemoteStream, context.Token);
            if (responseHeaderBytes != null)
            {
                var responseSocks4 = new Socks4Header(responseHeaderBytes);
                if (responseSocks4.CommandCode == (byte)Socks4Command.RequestGranted)
                {
                    if (context.Http != null)
                    {
                        if (context.Http.HTTPVerb == "CONNECT")
                        {
                            await SendHttpReply(context, Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
                        }
                        else
                        {
                            var firstLine = $"{context.Http?.HTTPVerb} {context.Http?.GetHTTPTargetPath()} {context.Http?.HTTPProtocol}";
                            var requestHeaderBytes = context.Http?.GetBytes(false, null, firstLine);
                            if (requestHeaderBytes != null && requestHeaderBytes.Length > 0)
                            {
                                await SendHttpRequest(context, requestHeaderBytes);
                            }
                        }
                    }
                    if (context.Socks5 != null)
                    {
                        await SendSocks5Reply(context, Socks5Status.Succeeded);
                    }
                    return HandleStep.Tunnel;
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

        public static Socks4ProxyHandler Instance()
        {
            return Self;
        }

        private static async Task SendHttpRequest(SessionContext context, byte[] requestBytes)
        {
            if (context.RemoteStream == null) return;
            await context.RemoteStream.WriteAsync(requestBytes, context.Token);
        }

        private static async Task SendSocks4Request(SessionContext context, byte[] requestBytes)
        {
            if (context.RemoteStream == null) return;
            await context.RemoteStream.WriteAsync(requestBytes, context.Token);
        }
    }
}
