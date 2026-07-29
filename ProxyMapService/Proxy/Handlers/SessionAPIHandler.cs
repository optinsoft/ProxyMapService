using Microsoft.AspNetCore.WebUtilities;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using System.Security.Cryptography.X509Certificates;
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

            if (path == "/")
            {
                await ShowDownloadPage(context, incomingStream);
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
            if (path == "/session/certificate")
            {
                await DownloadCertificate(context, incomingStream);
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

        private static async Task DownloadCertificate(SessionContext context, Stream incomingStream)
        {
            try
            {
                if (context.CACertificate == null)
                {
                    context.Logger.LogError("CA Certificate is missing in SessionContext.");
                    await HttpProto.HttpReplyNotFound(context, incomingStream);
                    return;
                }
                byte[] certBytes = context.CACertificate.Export(X509ContentType.Cert);
                await HttpProto.HttpReplyFileBytes(context, incomingStream, "ProxyMapRoot.crt", "application/x-x509-ca-cert", certBytes);
            }
            catch (Exception ex)
            {
                context.Logger.LogError("Error downloading CA certificate: {Message}", ex.Message);
                await HttpProto.HttpReplyInternalServerError(context, incomingStream, "Error downloading CA certificate");
            }
        }

        private static async Task ShowDownloadPage(SessionContext context, Stream incomingStream)
        {
            string html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Download Certificate</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 40px; background: #f5f5f7; color: #1d1d1f; text-align: center; }
        .container { max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 12px; box-shadow: 0 4px 6px rgba(0,0,0,0.05); }
        h1 { font-size: 24px; margin-bottom: 20px; }
        p { font-size: 16px; line-height: 1.5; color: #515154; }
        a { color: #0066cc; text-decoration: none; font-weight: 600; }
        a:hover { text-decoration: underline; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Certificate Installation</h1>
        <p>You can download the <a href='/session/certificate'>ProxyMapService Root CA Certificate</a></p>
    </div>
</body>
</html>";
            await HttpProto.HttpReplyHtml(context, incomingStream, html);
        }
    }
}