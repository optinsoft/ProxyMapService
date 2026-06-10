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
                    ExpiresAt = context.UsernameParameterResolver.CurrentSessionExpiresAt.HasValue ? $"{context.UsernameParameterResolver.CurrentSessionExpiresAt.Value.ToUniversalTime():R}" : null,
                    Expired = context.UsernameParameterResolver.CurrentSessionExpired,
                };
                string[] headers = [
                    $"X-Session-Id: \"{response.SessionId}\"",
                    $"X-Expires-At: {response.ExpiresAt ?? "null"}",
                    $"X-Expired: {response.Expired}"
                ];
                await HttpProto.HttpReplyJson(context, response, headers);
            }
            return HandleStep.Terminate;
        }

        public static GetSessionHandler Instance()
        {
            return Self;
        }
    }
}
