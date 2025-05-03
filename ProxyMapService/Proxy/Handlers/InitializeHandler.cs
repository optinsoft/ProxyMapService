using ProxyMapService.Proxy.Headers;
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
                context.ClientStream = context.Client.GetStream();
                var requestHeaderBytes = await context.ClientHeaderStream.ReadHeaderBytes(context.ClientStream, context.Token);
                if (requestHeaderBytes != null)
                {
                    switch (context.ClientHeaderStream.SocksVersion)
                    {
                        case 0x00:
                            context.Http = new HttpRequestHeader(requestHeaderBytes);
                            return HandleStep.HttpInitialized;
                        case 0x04:
                            context.Socks4 = new Socks4Header(requestHeaderBytes);
                            if (context.Socks4.IsConnectRequest(requestHeaderBytes))
                            {
                                return HandleStep.Socks4Initialized;
                            }
                            break;
                        case 0x05:
                            context.Socks5 = new Socks5Header(requestHeaderBytes);
                            return HandleStep.Socks5Initialized;
                    }
                }
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnHeaderFailed(context);
                throw;
            }
            context.SessionsCounter?.OnHeaderFailed(context);
            return HandleStep.Terminate;
        }

        public static InitializeHandler Instance()
        {
            return Self;
        }
    }
}
