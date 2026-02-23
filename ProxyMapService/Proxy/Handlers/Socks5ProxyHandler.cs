using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Net.Sockets;
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
                        hostEndPoint = Address.GetIPEndPoint(context.HostName, context.HostPort);
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
                        : Socks5Header.GetConnectRequestBytes(context.HostName, context.HostPort);

                    await SendSocks5Request(context, requestBytes);

                    if (context.Socks5 != null)
                    {
                        return HandleStep.Tunnel;
                    }

                    var socks5Reply = await ReadSocks5Reply(context);
                    status = socks5Reply != null && socks5Reply[0] == 0x05 ? (Socks5Status)socks5Reply[1] : Socks5Status.GeneralFailure;

                    if (status == Socks5Status.Succeeded)
                    {
                        if (context.Http != null)
                        {
                            if (context.Http.HTTPVerb == "CONNECT")
                            {
                                await HttpReply(context, Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
                            }
                            else
                            {
                                var requestFirstLine = $"{context.Http.HTTPVerb} {context.Http.HTTPTargetPath} {context.Http.HTTPProtocol}";
                                var httpRequestBytes = context.Http.GetBytes(false, null, requestFirstLine);
                                if (httpRequestBytes != null && httpRequestBytes.Length > 0)
                                {
                                    await SendHttpRequest(context, httpRequestBytes);
                                }
                            }
                        }
                        if (context.Socks4 != null)
                        {
                            await Socks4Reply(context, Socks4Command.RequestGranted);
                        }
                        return HandleStep.Tunnel;
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

        public static Socks5ProxyHandler Instance()
        {
            return Self;
        }

        private static async Task SendHttpRequest(SessionContext context, byte[] requestBytes)
        {
            if (context.OutgoingStream == null) return;
            await context.OutgoingStream.WriteAsync(requestBytes, context.Token);
        }

        private static async Task<Socks5Status> Socks5Auth(SessionContext context, string? username, string? password)
        {
            var requestBytes = Socks5Header.GetMethodsBytes(username, password);
            await SendSocks5Request(context, requestBytes);
            byte[]? authMethod = await ReadSocks5(context, 2);
            if (authMethod == null || authMethod[0] != 0x05)
            {
                return Socks5Status.NetworkUnreachable;
            }
            if (authMethod[1] == 0x02)
            {
                requestBytes = Socks5Header.GetUsernamePasswordBytes(username, password);
                await SendSocks5Request(context, requestBytes);
                byte[]? authResult = await ReadSocks5(context, 2);
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

        private static async Task SendSocks5Request(SessionContext context, byte[] requestBytes)
        {
            if (context.OutgoingStream == null) return;
            await context.OutgoingStream.WriteAsync(requestBytes, context.Token);
        }

        private static async Task<byte[]?> ReadSocks5(SessionContext context, int length)
        {
            if (context.OutgoingStream == null) return null;
            byte[] readBuffer = new byte[length];
            int bufferPos = 0, bytesRead;
            context.OutgoingStream.PauseReadCount();
            try
            {
                do
                {
                    bytesRead = await context.OutgoingStream.ReadAsync(readBuffer.AsMemory(bufferPos, 1), context.Token);
                    if (bytesRead <= 0) return null;
                    bufferPos += 1;
                } while (bufferPos < length);
            }
            finally
            {
                if (bufferPos > 0)
                {
                    context.OutgoingStream.OnBytesRead(bufferPos, readBuffer, 0);
                }
                context.OutgoingStream.ResumeReadCount();
            }
            return readBuffer;
        }

        private static async Task<byte[]?> ReadSocks5Reply(SessionContext context)
        {
            if (context.OutgoingStream == null) return null;
            int readLength = 4;
            byte[] readBuffer = new byte[readLength];
            int bufferPos = 0, bytesRead;
            byte atyp = 0;
            do
            {
                bytesRead = await context.OutgoingStream.ReadAsync(readBuffer.AsMemory(bufferPos, 1), context.Token);
                if (bytesRead <= 0) return null;
                if (bufferPos == 3)
                {
                    atyp = readBuffer[bufferPos];
                    switch (atyp)
                    {
                        case 0x01:
                            readLength = 10;
                            Array.Resize(ref readBuffer, readLength);
                            break;
                        case 0x03:
                            readLength = 5;
                            Array.Resize(ref readBuffer, readLength);
                            break;
                        case 0x04:
                            readLength = 22;
                            Array.Resize(ref readBuffer, readLength);
                            break;
                    }
                }
                else if (bufferPos == 4 && atyp == 0x03)
                {
                    int alen = (int)readBuffer[bufferPos];
                    readLength = 7 + alen;
                    Array.Resize(ref readBuffer, readLength);
                }
                bufferPos += 1;
            } while (bufferPos < readLength);
            return readBuffer;
        }
    }
}
