using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class SessionAPIHandler : FileRequestHandler, IHandler
    {
        private static readonly SessionAPIHandler Self = new();
        
        public static new SessionAPIHandler Instance()
        {
            return Self;
        }

        protected override async Task<HandleStep> HandleRequest(SessionContext context, Stream incomingStream, HttpRequestHeader http)
        {
            if (http.HTTPVerb != "GET")
            {
                context.Logger.LogHttpMethodNotAllowed(http.HTTPVerb);
                await HttpProto.HttpReplyMethodNotAllowed(context, incomingStream);
                return HandleStep.Terminate;
            }

            if (http.HTTPTargetPath == "/session/")
            {
                await GetSession(context, incomingStream);
                return HandleStep.Terminate;
            }
            if (http.HTTPTargetPath == "/session/new")
            {
                await NewSession(context, incomingStream);
                return HandleStep.Terminate;
            }
            if (http.HTTPTargetPath == "/session/reset")
            {
                await ResetSession(context, incomingStream);
                return HandleStep.Terminate;
            }

            context.Logger.LogHttpNotFound(http.HTTPTargetPath);
            await HttpProto.HttpReplyNotFound(context, incomingStream);
            return HandleStep.Terminate;
        }

        private static async Task GetSession(SessionContext context, Stream incomingStream)
        {
            context.UsernameParameterResolver.PopulateContext(context);
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
            await HttpProto.HttpReplyJson(context, incomingStream, response, headers);
        }

        private static async Task NewSession(SessionContext context, Stream incomingStream)
        {
            context.UsernameParameterResolver.ResetSessionId();
            context.UsernameParameterResolver.PopulateContext(context);
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
            await HttpProto.HttpReplyJson(context, incomingStream, response, headers);
        }

        private static async Task ResetSession(SessionContext context, Stream incomingStream)
        {
            context.UsernameParameterResolver.ResetSessionId();
            var response = new
            {
                Success = true,
            };
            await HttpProto.HttpReplyJson(context, incomingStream, response);
        }
    }
}
