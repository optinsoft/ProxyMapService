using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4FileHandler: IHandler
    {
        private static readonly Socks4FileHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            await Socks4Proto.Socks4Reply(context, Socks4Command.RequestGranted);
            return HandleStep.HandleFileRequest;
        }

        public static Socks4FileHandler Instance()
        {
            return Self;
        }

    }
}
