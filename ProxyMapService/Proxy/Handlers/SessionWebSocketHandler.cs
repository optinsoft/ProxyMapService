using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ProxyMapService.Proxy.Handlers
{
    public partial class SessionWebSocketHandler : IHandler
    {
        private static readonly SessionWebSocketHandler Self = new();
        private const int BufferSize = 8192;

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
            var buffer = new byte[BufferSize];

            CancellationToken token = context.Token;

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", token);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string jsonString = Encoding.UTF8.GetString(buffer, 0, result.Count);

                        try
                        {
                            using var doc = JsonDocument.Parse(jsonString);
                            var root = doc.RootElement;
                            string action = root.GetProperty("action").GetString() ?? "";

                            if (action == "subscribe")
                            {
                                string pattern = root.GetProperty("pattern").GetString() ?? ".*";

                                context.SessionManager.AddOrUpdateUrlSubscription(sessionId, webSocket, pattern);

                                byte[] confirm = Encoding.UTF8.GetBytes($"{{\"status\":\"subscribed\",\"session_id\":\"{sessionId}\"}}");
                                await webSocket.SendAsync(new ArraySegment<byte>(confirm), WebSocketMessageType.Text, true, token);
                            }
                        }
                        catch (Exception ex)
                        {
                            byte[] error = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}");
                            await webSocket.SendAsync(new ArraySegment<byte>(error), WebSocketMessageType.Text, true, token);
                        }
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
    }
}
