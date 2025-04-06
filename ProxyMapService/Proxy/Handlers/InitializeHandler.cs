using Proxy.Headers;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class InitializeHandler : IHandler
    {
        private static readonly InitializeHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            try
            {
                context.ClientStream = context.Client.GetStream();
                context.Header = await HttpHeaderStream.Instance().GetHeader(context.ClientStream, context.Token);
            }
            catch(Exception)
            {
                context.SessionsCounter?.OnHeaderFailed();
                throw;
            }
            if (context.Header == null)
            {
                context.SessionsCounter?.OnHeaderFailed();
                return HandleStep.Terminate;
            }
            return HandleStep.Initialized;
        }

        public static InitializeHandler Instance()
        {
            return Self;
        }
    }
}
