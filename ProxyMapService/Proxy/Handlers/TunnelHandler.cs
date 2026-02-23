using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class TunnelHandler : IHandler
    {
        private static readonly TunnelHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Ssl)
            {
                return HandleStep.SslTunnel;
            }
            return HandleStep.RawTunnel;
        }

        public static TunnelHandler Instance()
        {
            return Self;
        }
    }
}
