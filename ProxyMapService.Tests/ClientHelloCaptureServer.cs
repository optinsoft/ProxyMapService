using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// A raw TCP server that captures the TLS ClientHello sent by the proxy's
    /// upstream (outgoing) connection and then closes the socket without
    /// completing the TLS handshake. Used to compute JA4 fingerprints of the
    /// ClientHello produced by SslStream or BoringSslStream.
    /// </summary>
    public sealed class ClientHelloCaptureServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly ConcurrentQueue<byte[]> _hellos = new();
        private readonly TaskCompletionSource<byte[]> _firstHello =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Port { get; }

        public ClientHelloCaptureServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public Task<byte[]> FirstHelloAsync(TimeSpan timeout) =>
            _firstHello.Task.WaitAsync(timeout);

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(token);
                }
                catch (Exception ex) when (
                    ex is OperationCanceledException or SocketException or ObjectDisposedException)
                {
                    break;
                }

                _ = Task.Run(() => HandleClientAsync(client, token), token);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using var tcp = client;
            await using var stream = tcp.GetStream();

            // Accumulate handshake bytes across TLS records until the full
            // ClientHello handshake message has been received.
            byte[]? handshake = null;
            int expectedLen = 0;

            while (true)
            {
                var header = new byte[5];
                if (!await ReadExactlyAsync(stream, header, token)) return;
                if (header[0] != 0x16) return;                          // not a handshake record

                int recordLen = (header[3] << 8) | header[4];
                var body = new byte[recordLen];
                if (!await ReadExactlyAsync(stream, body, token)) return;

                if (handshake == null)
                {
                    if (body.Length < 4 || body[0] != 0x01) return;     // not client_hello
                    expectedLen = 4 + ((body[1] << 16) | (body[2] << 8) | body[3]);
                    handshake = new byte[Math.Min(expectedLen, body.Length)];
                    Array.Copy(body, handshake, handshake.Length);
                }
                else
                {
                    int offset = handshake.Length;
                    int copy = Math.Min(expectedLen - offset, body.Length);
                    Array.Resize(ref handshake, offset + copy);
                    Array.Copy(body, 0, handshake, offset, copy);
                }

                if (handshake.Length >= expectedLen)
                {
                    // Strip the 4-byte handshake header: pass the ClientHello body
                    // (starting at client_version) to the consumer.
                    _hellos.Enqueue(handshake[4..]);
                    _firstHello.TrySetResult(handshake[4..]);
                    return;                                             // drop the connection: no TLS handshake
                }
            }
        }

        private static async Task<bool> ReadExactlyAsync(
            NetworkStream stream, byte[] buffer, CancellationToken token)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(read), token);
                if (n == 0) return false;
                read += n;
            }
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try { await _acceptLoop; } catch (OperationCanceledException) { }
            _cts.Dispose();
        }
    }
}
