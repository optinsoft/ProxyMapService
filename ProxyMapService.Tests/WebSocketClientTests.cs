using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// Tests how ProxyMapService behaves when a client opens a WebSocket (wss://)
    /// through the proxy: CONNECT tunnel -> TLS -> HTTP/1.1 Upgrade -> WebSocket frames.
    /// </summary>
    public class WebSocketClientTests
    {
        private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_TextEcho_ShouldWork()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5117, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var ws = CreateClientThroughProxy(configuration);
            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws"), token);

            Assert.Equal(WebSocketState.Open, ws.State);

            const string text = "Hello through ProxyMapService";
            await SendAsync(ws, Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, token);

            var (type, data) = await ReceiveMessageAsync(ws, token);

            Assert.Equal(WebSocketMessageType.Text, type);
            Assert.Equal(text, Encoding.UTF8.GetString(data));

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);

            Assert.Equal(WebSocketState.Closed, ws.State);
            Assert.Equal(1, server.WebSocketConnectionCount);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_BinaryEcho_ShouldWork()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5118, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var ws = CreateClientThroughProxy(configuration);
            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws"), token);

            // All 256 byte values, so nothing gets "fixed up" as text.
            var payload = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            await SendAsync(ws, payload, WebSocketMessageType.Binary, token);

            var (type, data) = await ReceiveMessageAsync(ws, token);

            Assert.Equal(WebSocketMessageType.Binary, type);
            Assert.Equal(payload, data);

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_ManyMessages_ShouldPreserveOrder()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5119, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var ws = CreateClientThroughProxy(configuration);
            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws"), token);

            const int count = 50;

            // Send everything first without waiting for replies: several frames may reach the proxy in one read.
            for (var i = 0; i < count; i++)
            {
                await SendAsync(ws, Encoding.UTF8.GetBytes($"Message {i}"), WebSocketMessageType.Text, token);
            }

            for (var i = 0; i < count; i++)
            {
                var (type, data) = await ReceiveMessageAsync(ws, token);

                Assert.Equal(WebSocketMessageType.Text, type);
                Assert.Equal($"Message {i}", Encoding.UTF8.GetString(data));
            }

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_LargeFragmentedMessage_ShouldWork()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5120, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var ws = CreateClientThroughProxy(configuration);
            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws"), token);

            // 256 KB: well above the proxy buffer size (8192), so it can't pass in a single read.
            var payload = RandomNumberGenerator.GetBytes(256 * 1024);

            // Send as one logical message made of several fragments (frames).
            const int chunkSize = 10_000;
            for (var offset = 0; offset < payload.Length; offset += chunkSize)
            {
                var length = Math.Min(chunkSize, payload.Length - offset);
                var isLast = offset + length >= payload.Length;

                await ws.SendAsync(
                    new ArraySegment<byte>(payload, offset, length),
                    WebSocketMessageType.Binary,
                    endOfMessage: isLast,
                    token);
            }

            var (type, data) = await ReceiveMessageAsync(ws, token);

            Assert.Equal(WebSocketMessageType.Binary, type);
            Assert.Equal(payload.Length, data.Length);
            Assert.True(payload.AsSpan().SequenceEqual(data), "Echoed payload differs from the sent one");

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_ServerPushThenClose_ShouldDeliverAllMessagesAndCloseStatus()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5121, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var ws = CreateClientThroughProxy(configuration);
            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws-push"), token);

            // The client doesn't send anything - only the server -> client direction through the proxy.
            for (var i = 1; i <= 3; i++)
            {
                var (type, data) = await ReceiveMessageAsync(ws, token);

                Assert.Equal(WebSocketMessageType.Text, type);
                Assert.Equal($"Push {i}", Encoding.UTF8.GetString(data));
            }

            // The next thing is the Close frame from the server.
            var closeResult = await ws.ReceiveAsync(new ArraySegment<byte>(new byte[256]), token);

            Assert.Equal(WebSocketMessageType.Close, closeResult.MessageType);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, closeResult.CloseStatus);
            Assert.Equal("Push complete", closeResult.CloseStatusDescription);
            Assert.Equal(WebSocketState.CloseReceived, ws.State);

            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", token);

            Assert.Equal(WebSocketState.Closed, ws.State);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_ServerInitiatedClose_ShouldPassCustomStatusCode()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5122, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var ws = CreateClientThroughProxy(configuration);
            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws-close"), token);

            var result = await ws.ReceiveAsync(new ArraySegment<byte>(new byte[256]), token);

            Assert.Equal(WebSocketMessageType.Close, result.MessageType);
            Assert.Equal((WebSocketCloseStatus)4001, result.CloseStatus);
            Assert.Equal("Closed by server", result.CloseStatusDescription);

            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", token);

            Assert.Equal(WebSocketState.Closed, ws.State);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_SubProtocol_ShouldBeNegotiated()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5123, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var ws = CreateClientThroughProxy(configuration);
            ws.Options.AddSubProtocol("chat.v1");
            ws.Options.AddSubProtocol("chat.v2");

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws-subprotocol"), token);

            // Sec-WebSocket-Protocol must pass through the proxy in both directions.
            Assert.Equal("chat.v2", ws.SubProtocol);

            await SendAsync(ws, Encoding.UTF8.GetBytes("ping"), WebSocketMessageType.Text, token);
            var (_, data) = await ReceiveMessageAsync(ws, token);
            Assert.Equal("ping", Encoding.UTF8.GetString(data));

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_HandshakeHeaders_ShouldBeForwardedToServer()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5124, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var ws = CreateClientThroughProxy(configuration);
            ws.Options.SetRequestHeader("X-Test-Header", "test-value-123");

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws-headers"), token);

            var (type, data) = await ReceiveMessageAsync(ws, token);

            Assert.Equal(WebSocketMessageType.Text, type);
            Assert.Equal("test-value-123", Encoding.UTF8.GetString(data));

            var closeResult = await ws.ReceiveAsync(new ArraySegment<byte>(new byte[256]), token);
            Assert.Equal(WebSocketMessageType.Close, closeResult.MessageType);

            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", token);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_ParallelConnections_ShouldBeIndependent()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5125, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            const int connections = 5;
            var uri = new Uri($"wss://{testHost}:{server.Port}/ws");

            var clients = Enumerable.Range(0, connections)
                .Select(_ => CreateClientThroughProxy(configuration))
                .ToArray();

            try
            {
                // Open all connections simultaneously: they must all be alive at the same time.
                await Task.WhenAll(clients.Select(c => c.ConnectAsync(uri, token)));

                Assert.All(clients, c => Assert.Equal(WebSocketState.Open, c.State));

                // Each connection gets only its own data.
                var tasks = clients.Select(async (client, index) =>
                {
                    for (var round = 0; round < 10; round++)
                    {
                        var message = $"client {index}, round {round}";

                        await SendAsync(client, Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, token);

                        var (_, data) = await ReceiveMessageAsync(client, token);

                        Assert.Equal(message, Encoding.UTF8.GetString(data));
                    }

                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
                }).ToArray();

                await Task.WhenAll(tasks);

                Assert.Equal(connections, server.WebSocketConnectionCount);
            }
            finally
            {
                foreach (var client in clients)
                {
                    client.Dispose();
                }
            }
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_NonWebSocketRequestToWsEndpoint_ShouldReturn400AndKeepProxyWorking()
        {
            var configuration = BuildConfiguration();
            var proxyHost = configuration["Proxy:Host"] ?? "127.0.0.1";
            var proxyPort = configuration.GetValue<int>("Proxy:Port", 5001);
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5126, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}"),
                UseProxy = true,
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using var client = new HttpClient(handler);
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

            var baseUrl = $"https://{testHost}:{server.Port}";

            // A plain GET (without Upgrade) to the WebSocket endpoint: the server answers 400.
            using var bad = await client.GetAsync($"{baseUrl}/ws");
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

            // The same keep-alive tunnel keeps working for ordinary requests.
            using var good = await client.GetAsync($"{baseUrl}/one");
            Assert.Equal(HttpStatusCode.OK, good.StatusCode);
            Assert.Equal("Response one", await good.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http11_RawHandshake_ShouldReturn101AndRelayFrames()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
            var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

            await using var server = new HttpsTestServer(5127, hostname: certHost, useHttp2: useHttp2);
            await server.StartAsync();

            var proxyHost = configuration["Proxy:Host"] ?? "127.0.0.1";
            var proxyPort = configuration.GetValue<int>("Proxy:Port", 5001);
            var authority = $"{testHost}:{server.Port}";

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var tcp = new TcpClient { NoDelay = true };
            await tcp.ConnectAsync(proxyHost, proxyPort, token);
            var network = tcp.GetStream();

            // 1. CONNECT
            var connect = Encoding.ASCII.GetBytes(
                $"CONNECT {authority} HTTP/1.1\r\nHost: {authority}\r\n\r\n");
            await network.WriteAsync(connect, token);

            var connectResponse = await ReadHeadersAsync(network, token);
            Assert.StartsWith("HTTP/1.1 200", connectResponse);

            // 2. TLS over the tunnel
            using var ssl = new SslStream(network, leaveInnerStreamOpen: false);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = testHost,
                RemoteCertificateValidationCallback = (_, _, _, _) => true // self-signed
            }, token);

            // 3. WebSocket handshake written by hand
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var handshake = Encoding.ASCII.GetBytes(
                $"GET /ws HTTP/1.1\r\n" +
                $"Host: {authority}\r\n" +
                $"Upgrade: websocket\r\n" +
                $"Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Key: {key}\r\n" +
                $"Sec-WebSocket-Version: 13\r\n\r\n");

            await ssl.WriteAsync(handshake, token);
            await ssl.FlushAsync(token);

            var handshakeResponse = await ReadHeadersAsync(ssl, token);

            Assert.StartsWith("HTTP/1.1 101", handshakeResponse);

            var expectedAccept = Convert.ToBase64String(
                SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid)));

            Assert.Contains(
                $"Sec-WebSocket-Accept: {expectedAccept}",
                handshakeResponse,
                StringComparison.OrdinalIgnoreCase);

            // 4. A text frame: client -> server frames are always masked.
            const string text = "raw frame";
            await ssl.WriteAsync(BuildMaskedFrame(0x1, Encoding.UTF8.GetBytes(text)), token);
            await ssl.FlushAsync(token);

            // Server -> client frames are not masked.
            var header = new byte[2];
            await ssl.ReadExactlyAsync(header, token);

            Assert.Equal(0x81, header[0]);          // FIN + text
            Assert.Equal(text.Length, header[1]);   // no mask, short length

            var payload = new byte[header[1]];
            await ssl.ReadExactlyAsync(payload, token);

            Assert.Equal(text, Encoding.UTF8.GetString(payload));

            // 5. Close handshake: status 1000 (0x03E8)
            await ssl.WriteAsync(BuildMaskedFrame(0x8, new byte[] { 0x03, 0xE8 }), token);
            await ssl.FlushAsync(token);

            await ssl.ReadExactlyAsync(header, token);

            Assert.Equal(0x88, header[0]);          // FIN + close
        }

        // ---------------------------------------------------------------

        private static IConfigurationRoot BuildConfiguration() =>
            new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

        private static CancellationTokenSource CreateTimeout() =>
            new CancellationTokenSource(TimeSpan.FromSeconds(15));

        private static ClientWebSocket CreateClientThroughProxy(IConfigurationRoot configuration)
        {
            var proxyHost = configuration["Proxy:Host"] ?? "127.0.0.1";
            var proxyPort = configuration.GetValue<int>("Proxy:Port", 5001);

            var ws = new ClientWebSocket();

            // For wss:// the client itself sends CONNECT to the proxy, then TLS, then the Upgrade request.
            ws.Options.Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}");
            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true; // self-signed

            return ws;
        }

        private static Task SendAsync(
            ClientWebSocket ws, byte[] data, WebSocketMessageType type, CancellationToken token) =>
            ws.SendAsync(new ArraySegment<byte>(data), type, endOfMessage: true, token);

        /// <summary>
        /// Reads one complete message (all fragments). Fails if a Close frame arrives instead.
        /// </summary>
        private static async Task<(WebSocketMessageType Type, byte[] Data)> ReceiveMessageAsync(
            ClientWebSocket ws, CancellationToken token)
        {
            var buffer = new byte[8192];
            using var ms = new MemoryStream();

            WebSocketReceiveResult result;

            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                Assert.NotEqual(WebSocketMessageType.Close, result.MessageType);

                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return (result.MessageType, ms.ToArray());
        }

        // A client -> server frame (RFC 6455): FIN=1, MASK=1, payload length < 126.
        private static byte[] BuildMaskedFrame(byte opcode, byte[] payload)
        {
            Assert.True(payload.Length < 126);

            var mask = RandomNumberGenerator.GetBytes(4);
            var frame = new byte[2 + 4 + payload.Length];

            frame[0] = (byte)(0x80 | opcode);
            frame[1] = (byte)(0x80 | payload.Length);
            Buffer.BlockCopy(mask, 0, frame, 2, 4);

            for (var i = 0; i < payload.Length; i++)
            {
                frame[6 + i] = (byte)(payload[i] ^ mask[i % 4]);
            }

            return frame;
        }

        // Read byte by byte to avoid "eating" the WebSocket frames that follow \r\n\r\n.
        private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken token)
        {
            var sb = new StringBuilder();
            var one = new byte[1];

            while (!sb.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                int n = await stream.ReadAsync(one, token);
                if (n == 0)
                {
                    throw new EndOfStreamException("Connection closed while reading headers");
                }
                sb.Append((char)one[0]);
            }

            return sb.ToString();
        }
    }
}
