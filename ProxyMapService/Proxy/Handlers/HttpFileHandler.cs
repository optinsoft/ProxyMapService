using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpFileHandler: IHandler
    {
        private static readonly HttpFileHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            return HandleStep.Terminate;
        }

        public static HttpFileHandler Instance()
        {
            return Self;
        }
    }
}
