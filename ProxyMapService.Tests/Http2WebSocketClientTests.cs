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
    public class Http2WebSocketClientTests
    {
        private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";


        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http2_TextEcho_ShouldWork()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5128, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var invoker = CreateMessageInvoker(configuration);

            using var ws = CreateHttp2ClientWebSocket();

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws"), invoker, token);

            Assert.Equal(WebSocketState.Open, ws.State);

            const string text = "Hello over HTTP/2";
            await SendAsync(ws, Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, token);

            var (type, data) = await ReceiveMessageAsync(ws, token);

            Assert.Equal(WebSocketMessageType.Text, type);
            Assert.Equal(text, Encoding.UTF8.GetString(data));

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);

            Assert.Equal(WebSocketState.Closed, ws.State);
            Assert.Equal(1, server.WebSocketConnectionCount);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http2_BinaryEcho_ShouldWork()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5129, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var invoker = CreateMessageInvoker(configuration);

            using var ws = CreateHttp2ClientWebSocket();

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws"), invoker, token);

            // All 256 byte values, so nothing gets "fixed up" as text.
            var payload = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            await SendAsync(ws, payload, WebSocketMessageType.Binary, token);

            var (type, data) = await ReceiveMessageAsync(ws, token);

            Assert.Equal(WebSocketMessageType.Binary, type);
            Assert.Equal(payload, data);

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http2_ManyMessages_ShouldPreserveOrder()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5130, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var invoker = CreateMessageInvoker(configuration);

            using var ws = CreateHttp2ClientWebSocket();

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws"), invoker, token);

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
        public async Task WebSocket_Proxy_Wss_Over_Http2_LargeFragmentedMessage_ShouldWork()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5131, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var invoker = CreateMessageInvoker(configuration);

            using var ws = CreateHttp2ClientWebSocket();

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws"), invoker, token);

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
        public async Task WebSocket_Proxy_Wss_Over_Http2_ServerPushThenClose_ShouldDeliverAllMessagesAndCloseStatus()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5132, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var invoker = CreateMessageInvoker(configuration);

            using var ws = CreateHttp2ClientWebSocket();

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws-push"), invoker, token);

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
        public async Task WebSocket_Proxy_Wss_Over_Http2_ServerInitiatedClose_ShouldPassCustomStatusCode()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5133, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var invoker = CreateMessageInvoker(configuration);

            using var ws = CreateHttp2ClientWebSocket();

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws-close"), invoker, token);

            var result = await ws.ReceiveAsync(new ArraySegment<byte>(new byte[256]), token);

            Assert.Equal(WebSocketMessageType.Close, result.MessageType);
            Assert.Equal((WebSocketCloseStatus)4001, result.CloseStatus);
            Assert.Equal("Closed by server", result.CloseStatusDescription);

            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", token);

            Assert.Equal(WebSocketState.Closed, ws.State);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http2_SubProtocol_ShouldBeNegotiated()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5134, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var invoker = CreateMessageInvoker(configuration);

            using var ws = CreateHttp2ClientWebSocket();

            ws.Options.AddSubProtocol("chat.v1");
            ws.Options.AddSubProtocol("chat.v2");

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws-subprotocol"), invoker, token);

            // Sec-WebSocket-Protocol must pass through the proxy in both directions.
            Assert.Equal("chat.v2", ws.SubProtocol);

            await SendAsync(ws, Encoding.UTF8.GetBytes("ping"), WebSocketMessageType.Text, token);
            var (_, data) = await ReceiveMessageAsync(ws, token);
            Assert.Equal("ping", Encoding.UTF8.GetString(data));

            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", token);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http2_HandshakeHeaders_ShouldBeForwardedToServer()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5135, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var invoker = CreateMessageInvoker(configuration);

            using var ws = CreateHttp2ClientWebSocket();

            ws.Options.SetRequestHeader("X-Test-Header", "test-value-123");

            await ws.ConnectAsync(new Uri($"wss://{testHost}:{server.Port}/ws-headers"), invoker, token);

            var (type, data) = await ReceiveMessageAsync(ws, token);

            Assert.Equal(WebSocketMessageType.Text, type);
            Assert.Equal("test-value-123", Encoding.UTF8.GetString(data));

            var closeResult = await ws.ReceiveAsync(new ArraySegment<byte>(new byte[256]), token);
            Assert.Equal(WebSocketMessageType.Close, closeResult.MessageType);

            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", token);
        }

        [Fact]
        public async Task WebSocket_Proxy_Wss_Over_Http2_ParallelConnections_ShouldBeIndependent()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5136, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            using var cts = CreateTimeout();
            var token = cts.Token;

            const int connections = 5;
            var uri = new Uri($"wss://{testHost}:{server.Port}/ws");

            using var invoker = CreateMessageInvoker(configuration);

            var clients = Enumerable.Range(0, connections)
                .Select(_ => CreateHttp2ClientWebSocket())
                .ToArray();

            try
            {
                // Open all connections simultaneously: they must all be alive at the same time.
                await Task.WhenAll(clients.Select(c => c.ConnectAsync(uri, invoker, token)));

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
        public async Task WebSocket_Proxy_Wss_Over_Http2_NonWebSocketRequestToWsEndpoint_ShouldReturn400AndKeepProxyWorking()
        {
            var configuration = BuildConfiguration();
            var proxyHost = configuration["Proxy:Host"] ?? "127.0.0.1";
            var proxyPort = configuration.GetValue<int>("Proxy:Port", 5001);
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5137, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}"),
                UseProxy = true,
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using var client = new HttpClient(handler);
            client.DefaultRequestVersion = HttpVersion.Version20;
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
        public async Task WebSocket_Proxy_Wss_Over_Http2_RawHandshake_ShouldReturn200AndRelayFrames()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
            var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;

            await using var server = new HttpsTestServer(5138, hostname: certHost, useHttp2: true);
            await server.StartAsync();

            var proxyHost = configuration["Proxy:Host"] ?? "127.0.0.1";
            var proxyPort = configuration.GetValue<int>("Proxy:Port", 5001);
            var authority = $"{testHost}:{server.Port}";

            using var cts = CreateTimeout();
            var token = cts.Token;

            using var tcp = new TcpClient { NoDelay = true };
            await tcp.ConnectAsync(proxyHost, proxyPort, token);
            var network = tcp.GetStream();

            // 1. CONNECT to the proxy (HTTP/1.1) – this is the only HTTP/1.1 step; what follows is a pure tunnel.
            var connect = Encoding.ASCII.GetBytes(
                $"CONNECT {authority} HTTP/1.1\r\nHost: {authority}\r\n\r\n");
            await network.WriteAsync(connect, token);

            var connectResponse = await ReadHeadersAsync(network, token);
            Assert.StartsWith("HTTP/1.1 200", connectResponse);

            // 2. TLS over a tunnel, negotiating h2 via ALPN.
            using var ssl = new SslStream(network, leaveInnerStreamOpen: false);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = testHost,
                ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http2 },
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }, token);

            Assert.Equal(SslApplicationProtocol.Http2, ssl.NegotiatedApplicationProtocol);

            var h2 = new Http2Connection(ssl, token);

            // 3. HTTP/2 connection preface + SETTINGS handshake.
            await h2.StartAsync();

            // 4. Extended CONNECT (RFC 8441) – the HTTP/2 equivalent of "Upgrade: websocket".
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var requestHeaders = new (string Name, string Value)[]
            {
                (":method", "CONNECT"),
                (":protocol", "websocket"),
                (":scheme", "https"),
                (":authority", authority),
                (":path", "/ws"),
                ("sec-websocket-version", "13"),
                ("sec-websocket-key", key),
            };

            await h2.WriteAsync(Http2Connection.BuildFrame(
                Http2FrameType.Headers,
                Http2Flags.EndHeaders,
                streamId: 1,
                Http2Connection.EncodeHeaders(requestHeaders)));

            // The server responds with :status 200 (not 101!) and keeps the stream open.
            var responseHeaders = await h2.WaitForHeadersAsync(streamId: 1);
            Assert.Equal("200", responseHeaders[":status"]);

            // 5. A masked WebSocket text frame within DATA. The server must send back
            //    an UNmasked frame.
            const string text = "raw frame";
            await h2.WriteAsync(Http2Connection.BuildFrame(
                Http2FrameType.Data,
                Http2Flags.None,
                streamId: 1,
                BuildMaskedFrame(0x1, Encoding.UTF8.GetBytes(text))));

            var echoData = await h2.WaitForDataAsync(streamId: 1);
            Assert.Equal(0x81, echoData[0]);          // FIN + text opcode
            Assert.Equal(text.Length, echoData[1]);   // no mask bit, short length
            Assert.Equal(text, Encoding.UTF8.GetString(echoData, 2, echoData.Length - 2));

            // 6. Close handshake: status 1000 = 0x03E8, also contained within DATA.
            await h2.WriteAsync(Http2Connection.BuildFrame(
                Http2FrameType.Data,
                Http2Flags.None,
                streamId: 1,
                BuildMaskedFrame(0x8, new byte[] { 0x03, 0xE8 })));

            var closeEcho = await h2.WaitForDataAsync(streamId: 1);
            Assert.Equal(0x88, closeEcho[0]);          // FIN + close opcode
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

        private static HttpMessageInvoker CreateMessageInvoker(IConfigurationRoot configuration)
        {
            var proxyHost = configuration["Proxy:Host"] ?? "127.0.0.1";
            var proxyPort = configuration.GetValue<int>("Proxy:Port", 5001);

            var handler = new SocketsHttpHandler
            {
                Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}"),
                UseProxy = true,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (_, _, _, _) => true
                }
            };

            return new HttpMessageInvoker(handler);
        }

        private static ClientWebSocket CreateHttp2ClientWebSocket()
        {
            var ws = new ClientWebSocket();
			
		    // Explicitly specify that we want to use WebSocket over HTTP/2
		    ws.Options.HttpVersion = HttpVersion.Version20;
		    ws.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;

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
