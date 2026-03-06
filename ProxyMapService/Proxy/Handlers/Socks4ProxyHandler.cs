using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Proto;
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
                string? clientUserId =
                    httpProxyAuthorization != null
                    ? httpProxyAuthorization[0]
                    : (!String.IsNullOrEmpty(context.Socks5?.Username) ? context.Socks5?.Username : context.Socks4?.UserId);
                socks4 = new Socks4Header(context.Host.Hostname, context.Host.Port, clientUserId);
            }

            context.OutgoingHeaderStream.SocksVersion = 0x04;

            string? userId = 
                !String.IsNullOrEmpty(context.ProxyServer?.Username) 
                ? GetContextProxyUsernameWithParameters(context)
                : (context.Mapping.Authentication.SetAuthentication 
                ? GetContextAuthenticationUsernameWithParameters(context)
                : (context.Mapping.Authentication.RemoveAuthentication ? null : socks4.UserId));

            var socks4ConnectBytes = Socks4Header.GetConnectRequestBytes(context.Host.Hostname, context.Host.Port, userId);
            await Socks4Proto.SendSocks4Request(context, socks4ConnectBytes);

            if (context.Socks4 != null)
            {
                return HandleStep.Tunnel;
            }

            var responseHeaderBytes = await context.OutgoingHeaderStream.ReadHeaderBytes(context.OutgoingStream, context.Token);
            if (responseHeaderBytes != null)
            {
                var responseSocks4 = new Socks4Header(responseHeaderBytes);
                if (responseSocks4.CommandCode == (byte)Socks4Command.RequestGranted)
                {
                    if (context.Http != null)
                    {
                        if (context.Http.HTTPVerb == "CONNECT")
                        {
                            await HttpProto.HttpReplyConnectionEstablished(context);
                        }
                        else
                        {
                            var requestFirstLine = $"{context.Http.HTTPVerb} {context.Http.HTTPTargetPath} {context.Http.HTTPProtocol}";
                            var httpRequestBytes = context.Http.GetBytes(false, null, requestFirstLine);
                            if (httpRequestBytes != null && httpRequestBytes.Length > 0)
                            {
                                await HttpProto.SendHttpRequest(context, httpRequestBytes);
                            }
                        }
                    }
                    if (context.Socks5 != null)
                    {
                        await Socks5Proto.Socks5ReplyStatus(context, Socks5Status.Succeeded);
                    }
                    return HandleStep.Tunnel;
                }
            }

            if (context.Http != null)
            {
                await HttpProto.HttpReplyBadGateway(context);
            }
            if (context.Socks4 != null)
            {
                await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed);
            }
            if (context.Socks5 != null)
            {
                await Socks5Proto.Socks5ReplyStatus(context, Socks5Status.GeneralFailure);
            }

            return HandleStep.Terminate;
        }

        public static Socks4ProxyHandler Instance()
        {
            return Self;
        }
    }
}
