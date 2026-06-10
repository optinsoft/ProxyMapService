using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4SessionAPIHandler: IHandler
    {
        private static readonly Socks4SessionAPIHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            await Socks4Proto.Socks4ReplyCommand(context, Socks4Command.RequestGranted);
            return HandleStep.HandleSessionAPI;
        }

        public static Socks4SessionAPIHandler Instance()
        {
            return Self;
        }
    }
}
