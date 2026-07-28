using Microsoft.AspNetCore.WebUtilities;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using System.Text.Json;

namespace ProxyMapService.Proxy.Handlers
{
    public class SessionAPIHandler : FileRequestHandler, IHandler
    {
        private static readonly SessionAPIHandler Self = new();
        
        public static new SessionAPIHandler Instance()
        {
            return Self;
        }

        protected override async Task<HandleStep> HandleRequest(SessionContext context, Stream incomingStream, 
            HttpRequestHeader http, MemoryStream bodyStream)
        {
            if (http.HTTPTargetPath == null)
            {
                context.Logger.LogHttpNotFound(http.HTTPTargetPath);
                await HttpProto.HttpReplyNotFound(context, incomingStream);
                return HandleStep.Terminate;
            }

            var pathAndQuery = http.HTTPTargetPath;
            int queryIndex = pathAndQuery.IndexOf('?');
            string path = queryIndex >= 0 ? pathAndQuery.Substring(0, queryIndex) : pathAndQuery;

            var queryParams = queryIndex >= 0
                ? QueryHelpers.ParseQuery(http.HTTPTargetPath.Substring(queryIndex))
                : new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();

            if (http.HTTPVerb == "POST" && path == "/session/new")
            {
                using var reader = new StreamReader(bodyStream, System.Text.Encoding.UTF8, leaveOpen: true);
                string jsonBody = await reader.ReadToEndAsync();

                Dictionary<string, string>? parameters = null;

                if (!string.IsNullOrWhiteSpace(jsonBody))
                {
                    try
                    {
                        using var jsonDocument = JsonDocument.Parse(jsonBody);
                        var root = jsonDocument.RootElement;
                        if (root.ValueKind == JsonValueKind.Object &&
                            root.TryGetProperty("UsernameParameters", out var nestedElement))
                        {
                            if (nestedElement.ValueKind == JsonValueKind.Object)
                            {
                                parameters = new();
                                foreach (var property in nestedElement.EnumerateObject()) 
                                {
                                    string value = property.Value.ValueKind switch
                                    {
                                        JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                                        JsonValueKind.Null => string.Empty,
                                        _ => property.Value.GetRawText()
                                    };
                                    parameters[property.Name] = value;
                                }
                            }
                        }

                    }
                    catch (JsonException ex)
                    {
                        context.Logger.LogInvalidJsonPayload(ex.Message);
                        await HttpProto.HttpReplyBadRequest(context, incomingStream);
                        return HandleStep.Terminate;
                    }
                }

                await NewSession(context, incomingStream, parameters);
                return HandleStep.Terminate;
            }

            if (http.HTTPVerb != "GET")
            {
                context.Logger.LogHttpMethodNotAllowed(http.HTTPVerb);
                await HttpProto.HttpReplyMethodNotAllowed(context, incomingStream);
                return HandleStep.Terminate;
            }

            if (path == "/session/")
            {
                await GetSession(context, incomingStream);
                return HandleStep.Terminate;
            }
            if (path == "/session/new")
            {
                Dictionary<string, string>? parameters = queryParams.Count > 0 
                    ? queryParams.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.ToString()) 
                    : null;
                await NewSession(context, incomingStream, parameters);
                return HandleStep.Terminate;
            }
            if (path == "/session/reset")
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
            var info = context.UsernameParameterResolver.CurrentSessionInfo;
            string[] headers = [
                $"X-Session-Id: {info.SessionId ?? "null"}",
                info.SessionTime.HasValue ? $"X-Session-Time: {info.SessionTime.Value}" : "X-Session-Time: null",
                info.ExpiresAt.HasValue ? $"X-Expires-At: {info.ExpiresAt.Value.ToUniversalTime():R}" : "X-Expires-At: null"
            ];
            await HttpProto.HttpReplyJson(context, incomingStream, info, headers);
        }

        private static async Task NewSession(SessionContext context, Stream incomingStream, Dictionary<string, string>? parameters)
        {
            context.UsernameParameterResolver.ResetSessionId();
            if (parameters != null)
            {
                context.UsernameParameters ??= new();
                foreach (var param in parameters)
                {
                    context.UsernameParameters.SetValue(param.Key, param.Value);
                }
            }
            context.UsernameParameterResolver.PopulateContext(context);
            var info = context.UsernameParameterResolver.CurrentSessionInfo;
            string[] headers = [
                $"X-Session-Id: {info.SessionId ?? "null"}",
                info.SessionTime.HasValue ? $"X-Session-Time: {info.SessionTime.Value}" : "X-Session-Time: null",
                info.ExpiresAt.HasValue ? $"X-Expires-At: {info.ExpiresAt.Value.ToUniversalTime():R}" : "X-Expires-At: null"
            ];
            await HttpProto.HttpReplyJson(context, incomingStream, info, headers);
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
