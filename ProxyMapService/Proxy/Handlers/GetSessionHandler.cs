using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class GetSessionHandler: IHandler
    {
        private static readonly GetSessionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && !context.Token.IsCancellationRequested)
            {
                var response = new
                { 
                    SessionId = context.UsernameParameterResolver.CurrentSessionId,
                    ExpiresAt = context.UsernameParameterResolver.CurrentSessionExpiresAt?.ToUniversalTime(),
                    Expired = context.UsernameParameterResolver.CurrentSessionExpired,
                };
                await HttpProto.HttpReplyJson(context.IncomingStream, response, context.Token);
            }
            return HandleStep.Terminate;
        }

        public static GetSessionHandler Instance()
        {
            return Self;
        }
    }
}
