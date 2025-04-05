using Proxy.Headers;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class InitializeHandler : IHandler
    {
        private static readonly InitializeHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.ClientStream = context.Client.GetStream();
            context.Header = await HttpHeaderStream.Instance().GetHeader(context.ClientStream, context.Token);
            return context.Header != null ? HandleStep.Initialized : HandleStep.Terminate;
        }

        public static InitializeHandler Instance()
        {
            return Self;
        }
    }
}
