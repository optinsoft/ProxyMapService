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

            WsConnection conn = new(webSocket);

            string sessionId = !string.IsNullOrEmpty(context.SessionId) ? context.SessionId : $":{context.Mapping.Listen.Port}"; // : Guid.NewGuid().ToString();

            CancellationToken token = context.Token;

            try
            {
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
                            await SendJsonAsync(conn, new { error = "missing_action" }, token);
                            continue;
                        }

                        switch (actionEl.GetString())
                        {
                            case "subscribe":
                                string pattern = root.TryGetProperty("pattern", out var p) && p.ValueKind == JsonValueKind.String
                                    ? p.GetString()! : ".*";
                                var newSubId = context.SessionManager.AddUrlSubscription(conn, sessionId, pattern);
                                if (newSubId == null)
                                {
                                    await SendJsonAsync(conn, new { error = "too_many_subscriptions" }, token);
                                }
                                else
                                {
                                    await SendJsonAsync(conn, new { status = "subscribed", session_id = sessionId, sub_id = newSubId }, token);
                                }
                                break;
                            case "unsubscribe":
                                if(!root.TryGetProperty("sub_id", out var s) || s.ValueKind != JsonValueKind.String)
        {
                                    await SendJsonAsync(conn, new { error = "missing_sub_id" }, token);
                                    break;
                                }
                                string subId = s.GetString()!;
                                if (context.SessionManager.RemoveUrlSubscription(conn, subId)) 
                                {
                                    await SendJsonAsync(conn, new { status = "unsubscribed", sub_id = subId }, token);
                                }
                                else
                                {
                                    await SendJsonAsync(conn, new { error = "unknown_sub_id", sub_id = subId }, token);
                                }
                                break;
                            default:
                                await SendJsonAsync(conn, new { error = "unknown_action" }, token);
                                break;
                        }
                    }
                    catch(JsonException)
                    {
                        await SendJsonAsync(conn, new { error = "invalid_json" }, token);
                    }
                    catch (ArgumentException) // RegexParseException inherits from ArgumentException
                    {
                        await SendJsonAsync(conn, new { error = "invalid_pattern" }, token);
                    }
                    catch (OperationCanceledException) { 
                        throw; // don't send error to the cancelled socket
                    }
                    catch (Exception)
                    {
                        await SendJsonAsync(conn, new { error = "internal_error" }, token);
                    }
                }
            }
            finally
            {
                context.SessionManager.RemoveConnectionSubscriptions(conn);
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
