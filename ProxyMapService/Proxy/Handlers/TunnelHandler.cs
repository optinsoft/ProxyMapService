using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class TunnelHandler : IHandler
    {
        private static readonly TunnelHandler Self = new();
        private const int BufferSize = 8192;
        private static int _tunnelId = 0;

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Mapping.Listen.Ssl)
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
