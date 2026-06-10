using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5SessionAPIHandler: IHandler
    {
        private static readonly Socks5SessionAPIHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            await Socks5Proto.Socks5ReplyStatus(context, Socks5Status.Succeeded);
            return HandleStep.HandleSessionAPI;
        }

        public static Socks5SessionAPIHandler Instance()
        {
            return Self;
        }
    }
}
