using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5ProxyHandler : BaseProxyHandler, IHandler
    {
        private static readonly Socks5ProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            var socks5 = context.Socks5;

            if (socks5 == null)
            {
                var httpProxyAuthorization =
                    !String.IsNullOrEmpty(context.Http?.ProxyAuthorization)
                    ? Encoding.ASCII.GetString(Convert.FromBase64String(context.Http.ProxyAuthorization)).Split(':')
                    : null;
                string? clientUsername =
                    httpProxyAuthorization != null
                    ? httpProxyAuthorization[0]
                    : (!String.IsNullOrEmpty(context.Socks4?.UserId) ? context.Socks4?.UserId : null);
                string? clientPassword =
                     httpProxyAuthorization != null && httpProxyAuthorization.Length > 1
                    ? httpProxyAuthorization[1]
                    : null;
                socks5 = new Socks5Header(clientUsername, clientPassword);
            }

            context.OutgoingHeaderStream.SocksVersion = 0x05;

            string? username = 
                !String.IsNullOrEmpty(context.ProxyServer?.Username) 
                ? GetContextProxyUsernameWithParameters(context)
                : (context.Mapping.Authentication.SetAuthentication 
                ? GetContextAuthenticationUsernameWithParameters(context)
                : (context.Mapping.Authentication.RemoveAuthentication ? null : socks5.Username));
            string? password = 
                !String.IsNullOrEmpty(context.ProxyServer?.Password)
                ? context.ProxyServer.Password
                : (context.Mapping.Authentication.SetAuthentication 
                ? context.Mapping.Authentication.Password 
                : (context.Mapping.Authentication.RemoveAuthentication ? null : socks5.Password));

            Socks5Status status = await Socks5Auth(context, username, password);

            if (status == Socks5Status.Succeeded)
            {
                IPEndPoint? hostEndPoint = null;

                if (context.ProxyServer?.ResolveIP == true)
                {
                    try
                    {
                        hostEndPoint = context.Host.GetIPEndPoint();
                    }
                    catch (Exception)
                    {
                        status = Socks5Status.HostUnreachable;
                    }
                }

                if (status == Socks5Status.Succeeded)
                {
                    byte[] requestBytes = hostEndPoint != null
                        ? Socks5Header.GetConnectRequestBytes(hostEndPoint)
                        : Socks5Header.GetConnectRequestBytes(context.Host.Hostname, context.Host.Port);

                    await Socks5Proto.SendSocks5Request(context, requestBytes);

                    var socks5Reply = await Socks5Proto.ReadConnectReply(context);

                    if (context.Socks5 != null)
                    {
                        if (socks5Reply != null && socks5Reply.Length > 0)
                        {
                            if (context.IncomingStream != null)
                            {
                                await context.IncomingStream.WriteAsync(socks5Reply, context.Token);
                            }
                        }
                        return HandleStep.Tunnel;
                    }

                    status = socks5Reply != null && socks5Reply[0] == 0x05 ? (Socks5Status)socks5Reply[1] : Socks5Status.GeneralFailure;

                    if (status == Socks5Status.Succeeded)
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
                        if (context.Socks4 != null)
                        {
                            await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestGranted);
                        }
                        return HandleStep.Tunnel;
                    }
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

        public static Socks5ProxyHandler Instance()
        {
            return Self;
        }

        private static async Task<Socks5Status> Socks5Auth(SessionContext context, string? username, string? password)
        {
            var requestBytes = Socks5Header.GetMethodsBytes(username, password);
            await Socks5Proto.SendSocks5Request(context, requestBytes);
            byte[]? authMethod = await Socks5Proto.ReadSocks5Reply(context, 2);
            if (authMethod == null || authMethod[0] != 0x05)
            {
                return Socks5Status.NetworkUnreachable;
            }
            if (authMethod[1] == 0x02)
            {
                requestBytes = Socks5Header.GetUsernamePasswordBytes(username, password);
                await Socks5Proto.SendSocks5Request(context, requestBytes);
                byte[]? authResult = await Socks5Proto.ReadSocks5Reply(context, 2);
                if (authResult == null || authResult[0] != 0x01)
                {
                    return Socks5Status.NetworkUnreachable;
                }
                if (authResult[1] != 0x0)
                {
                    return Socks5Status.ConnectionNotAllowed;
                }
            }
            else if (authMethod[1] != 0x0)
            {
                return Socks5Status.ConnectionNotAllowed;
            }
            return Socks5Status.Succeeded;
        }
    }
}
