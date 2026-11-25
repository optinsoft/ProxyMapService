using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ws;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ProxyMapService.Proxy.Handlers
{
    public partial class SessionWebSocketHandler : IHandler
    {
        private static readonly SessionWebSocketHandler Self = new();
        private const int BufferSize = 8192;
        private const int MaxMessageSize = 64 * 1024;

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && !context.Token.IsCancellationRequested)
            {
                await ProcessWsSession(context, context.IncomingStream);
            }

            return HandleStep.Terminate;
        }

        private static async Task ProcessWsSession(SessionContext context, CountingStream incomingStream)
        {
            using WebSocket webSocket = WebSocket.CreateFromStream(
                incomingStream,
                isServer: true,
                subProtocol: null,
                keepAliveInterval: TimeSpan.FromSeconds(30)
            );

            string sessionId = !string.IsNullOrEmpty(context.SessionId) ? context.SessionId : $":{context.Mapping.Listen.Port}"; // : Guid.NewGuid().ToString();

            CancellationToken token = context.Token;

            try
            {
                WsConnection wsConnection = new(webSocket);
                while (webSocket.State == WebSocketState.Open)
                {
                    (WebSocketMessageType type, string? text) msg;
                    try
                    {
                        msg = await ReceiveMessageAsync(webSocket, token);
                    }
                    catch (InvalidDataException)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message too large", token);
                        break;
                    }

                    if (msg.type == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", token);
                        break;
                    }

                    if (msg.text == null) 
{
                        await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Text only", token);
                        break;
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(msg.text);
                        var root = doc.RootElement;

                        if (root.ValueKind != JsonValueKind.Object ||
                            !root.TryGetProperty("action", out var actionEl) ||
                            actionEl.ValueKind != JsonValueKind.String)
                        {
                            await SendJsonAsync(wsConnection, new { error = "missing_action" }, token);
                            continue;
                        }

                        switch (actionEl.GetString())
                        {
                            case "subscribe":
                                string pattern = root.TryGetProperty("pattern", out var p) && p.ValueKind == JsonValueKind.String
                                    ? p.GetString()! : ".*";
                                context.SessionManager.AddOrUpdateUrlSubscription(sessionId, wsConnection, pattern);
                                await SendJsonAsync(wsConnection, new { status = "subscribed", session_id = sessionId }, token);
                                break;
                            default:
                                await SendJsonAsync(wsConnection, new { error = "unknown_action" }, token);
                                break;
                        }
                    }
                    catch(JsonException)
                    {
                        await SendJsonAsync(wsConnection, new { error = "invalid_json" }, token);
                    }
                    catch (ArgumentException) // RegexParseException inherits from ArgumentException
                    {
                        await SendJsonAsync(wsConnection, new { error = "invalid_pattern" }, token);
                    }
                    catch (OperationCanceledException) { 
                        throw; // don't send error to the cancelled socket
                    }
                    catch (Exception)
                    {
                        await SendJsonAsync(wsConnection, new { error = "internal_error" }, token);
                    }
                }
            }
            finally
            {
                context.SessionManager.RemoveUrlSubscription(sessionId);
            }
        }

        public static SessionWebSocketHandler Instance()
        {
            return Self;
        }

        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        private static Task SendJsonAsync(WsConnection wsConnection, object payload, CancellationToken token)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
            return wsConnection.SendTextAsync(bytes, token);
        }

        private static async Task<(WebSocketMessageType Type, string? Text)> ReceiveMessageAsync(
            WebSocket ws, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                using var ms = new MemoryStream();
                ValueWebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(buffer.AsMemory(), token);
                    if (r.MessageType == WebSocketMessageType.Close)
                        return (WebSocketMessageType.Close, null);

                    ms.Write(buffer, 0, r.Count);
                    if (ms.Length > MaxMessageSize)
                        throw new InvalidDataException("Message too large");
                }
                while (!r.EndOfMessage);

                string? text = r.MessageType == WebSocketMessageType.Text
                    ? Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length)
                    : null;
                return (r.MessageType, text);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
