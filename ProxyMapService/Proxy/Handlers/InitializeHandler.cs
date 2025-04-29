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
                context.HeaderBytes = await context.ClientHeaderStream.ReadHeaderBytes(context.ClientStream, context.Token);
                if (context.HeaderBytes != null)
                {
                    switch (context.ClientHeaderStream.SocksVersion)
                    {
                        case 0x00:
                            context.Http = new HttpRequestHeader(context.HeaderBytes);
                            return HandleStep.HttpInitialized;
                        case 0x04:
                            context.Socks4 = new Socks4Header(context.HeaderBytes);
                            if (context.Socks4.IsConnectRequest(context.HeaderBytes))
                            {
                                return HandleStep.Socks4Initialized;
                            }
                            break;
                        case 0x05:
                            context.Socks5 = new Socks5Header(context.HeaderBytes);
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
