using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Text;
using static ProxyMapService.Proxy.Utils.ProxyHandlerUtils;
using static ProxyMapService.Proxy.Utils.CacheUtils;
using static ProxyMapService.Proxy.Utils.HttpBodyUtils;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4ProxyHandler : IHandler
    {
        private static readonly Socks4ProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            var socks4 = context.Socks4;

            System.Net.IPEndPoint? hostEndPoint = null;

            try
            {
                hostEndPoint = await context.Host.GetIPEndPoint();
            }
            catch (Exception ex)
            {
                context.Logger.LogHostError(ex.Message, context.Host.Hostname);
            }

            if (hostEndPoint != null)
            {
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
                    socks4 = new Socks4Header(hostEndPoint, clientUserId);
                }

                context.OutgoingHeaderStream.SocksVersion = 0x04;

                string? userId =
                    !String.IsNullOrEmpty(context.ProxyServer?.Username)
                    ? GetContextProxyUsernameWithParameters(context)
                    : (context.Mapping.Authentication.SetAuthentication
                    ? GetContextAuthenticationUsernameWithParameters(context)
                    : (context.Mapping.Authentication.RemoveAuthentication ? null : socks4.UserId));

                var socks4ConnectBytes = Socks4Header.GetConnectRequestBytes(hostEndPoint, userId);
                await Socks4Proto.SendSocks4Request(context, socks4ConnectBytes);

                var responseHeaderBytes = await context.OutgoingHeaderStream.ReadHeaderBytes(context.OutgoingStream, context.Token);

                var responseSocks4 = responseHeaderBytes != null ? new Socks4Header(responseHeaderBytes): null;

                var command = (Socks4Command)(responseSocks4?.CommandCode ?? 0);

                if (command == Socks4Command.RequestGranted)
                {
                    context.Logger.LogServerConnectedViaSocks4Proxy(context.Host, context.ProxyServer);
                }
                else
                {
                    context.Logger.LogSocks4ConnectionFailed(command, context.Host, context.ProxyServer);
                }

                if (context.Socks4 != null)
                {
                    if (responseHeaderBytes != null && responseHeaderBytes.Length > 0)
                    {
                        if (context.IncomingStream != null)
                        {
                            await context.IncomingStream.WriteAsync(responseHeaderBytes, context.Token);
                        }
                        return HandleStep.Tunnel;
                    }
                    // if responseHeaderBytes == null then responseSocks4 == null, return RequestRejectedOrFailed error and terminate
                }

                if (responseSocks4 != null)
                {
                    if (command == Socks4Command.RequestGranted)
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
                                var httpRequestBytes = context.Http.GetBytes(false, null, requestFirstLine, context.Host);
                                if (httpRequestBytes != null && httpRequestBytes.Length > 0)
                                {
                                    context.RequestHeader = new HttpRequestHeader(httpRequestBytes, null);
                                    if (!context.RequestHeader.BadRequest)
                                    {
                                        CreateRequestBodyTracker(context, null);
                                    }
                                    var cacheEntry = await GetCacheEntry(context);
                                    if (cacheEntry != null)
                                    {
                                        using FileStream? cacheFileStream = GetCacheEntryFileStream(cacheEntry);
                                        if (cacheFileStream != null)
                                        {
                                            context.RequestTunnelState.ResetReadHeaders = true;
                                            context.ProxyCounters.SessionsCounter?.OnCacheResponse(context);
                                            await HttpProto.HttpReplyCacheFileStream(context, cacheEntry, cacheFileStream);
                                            context.Logger.LogResponseFromCache(cacheFileStream.Name);
                                        }
                                        else
                                        {
                                            await HttpProto.SendHttpRequest(context, httpRequestBytes);
                                        }
                                    }
                                    else
                                    {
                                        await HttpProto.SendHttpRequest(context, httpRequestBytes);
                                    }
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
