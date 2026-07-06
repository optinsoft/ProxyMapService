using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Text;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;
using static ProxyMapService.Proxy.Utils.ProxyHandlerUtils;
using static ProxyMapService.Proxy.Utils.CacheUtils;
using static ProxyMapService.Proxy.Utils.HttpBodyUtils;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpProxyHandler : IHandler
    {
        private static readonly HttpProxyHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            var http = context.Http;
            if (http == null)
            {
                List<string> connectHttpCommand = [
                    $"CONNECT {context.Host} HTTP/1.1",
                    $"Host: {context.Host}"

                ];
                if (!string.IsNullOrEmpty(context.UserAgent))
                {
                    connectHttpCommand.Add($"User-Agent: {context.UserAgent}");
                }
                connectHttpCommand.Add("Proxy-Connection: Keep-Alive");
                http = new HttpRequestHeader([.. connectHttpCommand]);
            }

            string? requestFirstLine = null;
            var hostError = false;

            if (http.HTTPVerb == "CONNECT" && context.ProxyServer?.ResolveIP == true)
            {
                try
                {
                    System.Net.IPEndPoint hostEndPoint = await context.Host.GetIPEndPoint();
                    requestFirstLine = $"{http.HTTPVerb} {hostEndPoint.Address}:{hostEndPoint.Port} {http.HTTPProtocol}";
                }
                catch (Exception ex)
                {
                    hostError = true;
                    context.Logger.LogHostError(ex.Message, context.Host.Hostname);
                }
            }

            if (!hostError)
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

                var httpRequestBytes = http.GetBytes(true, proxyAuthorization, requestFirstLine, context.Host);
                if (httpRequestBytes != null && httpRequestBytes.Length > 0)
                {
                    context.RequestHeader = new HttpRequestHeader(httpRequestBytes);
                    if (context.Http == null)
                    {
                        context.RequestHeadersLogger?.OnHttpHeader(context, context.RequestHeader);
                    }

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
                            if (context.Socks4 != null)
                            {
                                await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestGranted);
                            }
                            if (context.Socks5 != null)
                            {
                                await Socks5Proto.Socks5ReplyStatus(context, Socks5Status.Succeeded);
                            }
                            await HttpProto.HttpReplyCacheFileStream(context, cacheEntry, cacheFileStream);
                            context.Logger.LogResponseFromCache(cacheFileStream.Name);
                            return HandleStep.Tunnel;
                        }
                    }

                    await HttpProto.SendHttpRequest(context, httpRequestBytes);

                    if (http.HTTPVerb != "CONNECT")
                    {
                        return HandleStep.Tunnel;
                    }

                    var responseHeaderBytes = await context.OutgoingHeaderStream.ReadHeaderBytes(context.OutgoingStream, context.Token);

                    if (responseHeaderBytes != null)
                    {
                        context.RequestTunnelState.ResetReadHeaders = true;
                        context.ResponseHeader = new HttpResponseHeader(responseHeaderBytes);
                        context.ResponseHeadersLogger?.OnHttpHeader(context, context.ResponseHeader);
                        if (!context.ResponseHeader.BadResponse)
                        {
                            CreateResponseBodyTracker(context, null);
                            if (CreateResponseCacheFileStream(context))
                            {
                                if (context.ResponseCacheFileStream != null)
                                {
                                    await context.ResponseCacheFileStream.WriteAsync(responseHeaderBytes.AsMemory(0, responseHeaderBytes.Length));
                                    await HandleEndOfResponseCacheFileStream(context);
                                }
                            }
                        }
                    }

                    var responseHttp = responseHeaderBytes != null ? context.ResponseHeader : null;

                    if (http.HTTPVerb == "CONNECT")
                    {
                        if (responseHttp?.StatusCode == "200")
                        {
                            context.Logger.LogServerConnectedViaHttpProxy(context.Host, context.ProxyServer);
                        }
                        else
                        {
                            context.Logger.LogHttpConnectionFailed(responseHttp, context.Host, context.ProxyServer);
                        }
                    }

                    if (context.Http != null)
                    {
                        if (responseHeaderBytes != null && responseHeaderBytes.Length > 0)
                        {
                            if (context.IncomingStream != null)
                            {
                                await context.IncomingStream.WriteAsync(responseHeaderBytes, context.Token);
                            }
                        }
                        return HandleStep.Tunnel;
                    }

                    if (responseHttp != null)
                    {
                        if (responseHttp.StatusCode == "200")
                        {
                            if (context.Socks4 != null)
                            {
                                await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestGranted);
                            }
                            if (context.Socks5 != null)
                            {
                                await Socks5Proto.Socks5ReplyStatus(context, Socks5Status.Succeeded);
                            }
                            return HandleStep.Tunnel;
                        }
                        context.Logger.LogHttpResponse(responseHttp.StatusCode, responseHttp.StatusText);
                        if (context.Socks4 != null)
                        {
                            await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestRejectedOrFailed);
                        }
                        if (context.Socks5 != null)
                        {
                            await Socks5Proto.Socks5ReplyStatus(context, Socks5Status.ConnectionNotAllowed);
                        }
                        return HandleStep.Terminate;
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

        public static HttpProxyHandler Instance()
        {
            return Self;
        }
    }
}
