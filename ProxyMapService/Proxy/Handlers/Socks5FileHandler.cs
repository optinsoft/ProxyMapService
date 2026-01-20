using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5FileHandler: IHandler
    {
        private static readonly Socks5FileHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            return HandleStep.Terminate;
        }

        public static Socks5FileHandler Instance()
        {
            return Self;
        }
    }
}
