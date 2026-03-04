using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpFileHandler: IHandler
    {
        private static readonly HttpFileHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Http?.HTTPVerb == "CONNECT")
            {
                await HttpProto.HttpReplyConnectionEstablished(context);
            }
            return HandleStep.HandleFileRequest;
        }

        public static HttpFileHandler Instance()
        {
            return Self;
        }
    }
}
