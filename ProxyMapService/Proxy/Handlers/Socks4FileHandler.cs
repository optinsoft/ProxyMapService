using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks4FileHandler: IHandler
    {
        private static readonly Socks4FileHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            return HandleStep.Terminate;
        }

        public static Socks4FileHandler Instance()
        {
            return Self;
        }

    }
}
