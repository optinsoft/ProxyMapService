using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using System.Net.Sockets;
using static ProxyMapService.Proxy.Utils.CacheUtils;
using static ProxyMapService.Proxy.Utils.HttpBodyUtils;

namespace ProxyMapService.Proxy.Handlers
{
    public partial class HttpBypassHandler: IHandler
    {
        private static readonly HttpBypassHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Bypassed = true;

            context.ProxyCounters.SessionsCounter?.OnHostBypassed(context);

            try
            {
                context.OutgoingEndPoint = await context.Host.GetIPEndPoint();
            }
            catch (Exception ex)
            {
                context.Logger.LogHostError(ex.Message, context.Host.Hostname);
                context.ProxyCounters.SessionsCounter?.OnBypassFailed(context);
                await HttpProto.HttpReplyBadGateway(context);
                return HandleStep.Terminate;
            }

            try
            {
                await context.OutgoingClient.ConnectAsync(context.OutgoingEndPoint, context.Token);
            }
            catch (SocketException ex)
            {
                context.ProxyCounters.SessionsCounter?.OnBypassFailed(context);
                switch (ex.SocketErrorCode)
                {
                    case SocketError.TimedOut:
                    case SocketError.TryAgain:
                        await HttpProto.HttpReplyGatewayTimeout(context, $"SocketError {ex.SocketErrorCode}");
                        break;
                    default:
                        await HttpProto.HttpReplyBadGateway(context, $"SocketError {ex.SocketErrorCode}");
                        break;
                }
                throw;
            }
            catch (Exception)
            {
                context.ProxyCounters.SessionsCounter?.OnBypassFailed(context);
                await HttpProto.HttpReplyBadGateway(context);
                throw;
            }

            context.Logger.LogBypassServerConnected(context.OutgoingEndPoint, context.Host);

            context.ProxyCounters.SessionsCounter?.OnBypassConnected(context);

            context.CreateOutgoingClientStream();
            if (context.OutgoingStream != null)
            {
                context.OutgoingStream.DisconnectHandler += HandlerLogger.OnBypassServerDisconnected;
            }

            if (context.Http?.HTTPVerb == "CONNECT")
            {
                await HttpProto.HttpReplyConnectionEstablished(context);
            }
            else
            {
                var requestFirstLine = $"{context.Http?.HTTPVerb} {context.Http?.HTTPTargetPath} {context.Http?.HTTPProtocol}";
                var httpRequestBytes = context.Http?.GetBytes(false, null, requestFirstLine, context.Host);
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
                            return HandleStep.Tunnel;
                        }
                    }
                    await HttpProto.SendHttpRequest(context, httpRequestBytes);
                }
            }

            return HandleStep.Tunnel;
        }
        
        public static HttpBypassHandler Instance()
        {
            return Self;
        }
    }
}
