using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5FileHandler: IHandler
    {
        private static readonly Socks5FileHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            await Socks5Proto.Socks5Reply(context, Socks5Status.Succeeded);
            return HandleStep.HandleFileRequest;
        }

        public static Socks5FileHandler Instance()
        {
            return Self;
        }
    }
}
