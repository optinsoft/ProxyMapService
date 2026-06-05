using Microsoft.Extensions.Logging;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

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
                if (requestHeaderBytes != null)
                {
                    switch (context.IncomingHeaderStream.SocksVersion)
                    {
                        case 0x00:
                            context.ConnectionType = ProxyType.Http;
                            context.Http = new HttpRequestHeader(requestHeaderBytes, context);
                            if (context.Http.BadRequest)
                            {
                                context.Logger.LogHttpBadRequest();
                                context.ProxyCounters.SessionsCounter?.OnHeaderFailed(context);
                                await HttpProto.HttpReplyBadRequest(context);
                                return HandleStep.Terminate;
                            }
                            return HandleStep.HttpInitialized;
                        case 0x04:
                            context.ConnectionType = ProxyType.Socks4;
                            context.Socks4 = new Socks4Header(requestHeaderBytes);
                            if (context.Socks4.IsConnectRequest(requestHeaderBytes))
                            {
                                return HandleStep.Socks4Initialized;
                            }
                            context.Logger.LogSocks4BadRequest(context.Socks4.CommandCode);
                            break;
                        case 0x05:
                            context.ConnectionType = ProxyType.Socks5;
                            context.Socks5 = new Socks5Header(requestHeaderBytes);
                            return HandleStep.Socks5Initialized;
                        default:
                            context.Logger.LogBadSocksVersion(context.IncomingHeaderStream.SocksVersion);
                            break;

                    }
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
