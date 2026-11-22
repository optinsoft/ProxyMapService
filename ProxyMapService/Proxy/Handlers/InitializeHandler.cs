using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Handlers
{
    public class InitializeHandler : IHandler
    {
        private static readonly InitializeHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            try
            {
                context.CreateIncomingClientStream();
                if (context.IncomingStream != null)
                {
                    context.IncomingStream.DisconnectHandler += HandlerLogger.OnClientDisconnected;
                }
                var requestHeaderBytes = await context.IncomingHeaderStream.ReadHeaderBytes(context.IncomingStream, context.Token);
                switch (context.IncomingHeaderStream.ProtocolType)
                {
                    case ProtocolTypeEnum.Http:
                        if (requestHeaderBytes == null)
                        {
                            context.Logger.LogReadHeaderFailed("HTTP");
                            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                            return HandleStep.Terminate;
                        }
                        context.InboundType = ProxyType.Http;
                        context.Http = new HttpRequestHeader(requestHeaderBytes);
                        if (context.Http.HTTPTargetHost != null)
                        {
                            //initialize context.Host before logging HttpRequestHeader
                            context.Host = context.Http.HTTPTargetHost;
                        }
                        // do not fall back to the Host header when determining the connection target.                        
                        context.RequestHeadersLogger?.OnHttpRequestHeader(context, context.Http);
                        if (context.Http.BadRequest)
                        {
                            context.Logger.LogHttpBadRequest();
                            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                            await HttpProto.HttpReplyBadRequest(context, context.Http);
                            return HandleStep.Terminate;
                        }
                        return HandleStep.HttpInitialized;
                    case ProtocolTypeEnum.Socks4:
                        if (requestHeaderBytes == null)
                        {
                            context.Logger.LogReadHeaderFailed("SOCKS4");
                            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                            return HandleStep.Terminate;
                        }
                        context.InboundType = ProxyType.Socks4;
                        context.Socks4 = new Socks4Header(requestHeaderBytes);
                        if (context.Socks4.IsConnectRequest(requestHeaderBytes))
                        {
                            return HandleStep.Socks4Initialized;
                        }
                        context.Logger.LogSocks4BadRequest(context.Socks4.CommandCode);
                        break;
                    case ProtocolTypeEnum.Socks5:
                        if (requestHeaderBytes == null)
                        {
                            context.Logger.LogReadHeaderFailed("SOCKS5");
                            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                            return HandleStep.Terminate;
                        }
                        context.InboundType = ProxyType.Socks5;
                        context.Socks5 = new Socks5Header(requestHeaderBytes);
                        return HandleStep.Socks5Initialized;
                    default:
                        context.Logger.LogUnknownProtocol();
                        break;
                }
            }
            catch (Exception)
            {
                context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                throw;
            }
            context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
            return HandleStep.Terminate;
        }

        public static InitializeHandler Instance()
        {
            return Self;
        }
    }
}
