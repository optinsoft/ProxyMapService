using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class ResetSessionHandler: IHandler
    {
        private static readonly ResetSessionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && !context.Token.IsCancellationRequested)
            {
                context.UsernameParameterResolver.ResetSessionId();
                var response = new
                {
                    Success = true,
                };
                await HttpProto.HttpReplyJson(context.IncomingStream, response, context.Token);
            }
            return HandleStep.Terminate;
        }

        public static ResetSessionHandler Instance()
        {
            return Self;
        }
    }
}
