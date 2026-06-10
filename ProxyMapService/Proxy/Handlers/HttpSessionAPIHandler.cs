using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpSessionAPIHandler: IHandler
    {
        private static readonly HttpSessionAPIHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Http?.HTTPVerb == "CONNECT")
            {
                await HttpProto.HttpReplyConnectionEstablished(context);
            }
            return HandleStep.HandleSessionAPI;
        }

        public static HttpSessionAPIHandler Instance()
        {
            return Self;
        }

    }
}
