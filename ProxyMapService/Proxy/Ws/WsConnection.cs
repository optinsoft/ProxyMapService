using System.Net.WebSockets;

namespace ProxyMapService.Proxy.Ws
{
    public sealed class WsConnection(WebSocket socket)
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        public WebSocket Socket { get; } = socket;

        public bool IsOpen() => Socket.State == WebSocketState.Open;

        public async Task SendTextAsync(byte[] bytes, CancellationToken token)
        {
            await _sendLock.WaitAsync(token);
            try
            {
                await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, token);
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
