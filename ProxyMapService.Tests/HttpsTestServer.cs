using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ProxyMapService.Tests
{
    public sealed class HttpsTestServer : IAsyncDisposable
    {
        private readonly int _port;

        public int Port { get { return _port; } }

        private readonly IHost _host;

        private int _connectionCount;
        private int _requestCount;
        private int _webSocketCount;

        public int ConnectionCount =>
            Volatile.Read(ref _connectionCount);

        public int RequestCount =>
            Volatile.Read(ref _requestCount);

        /// <summary>
        /// Number of WebSocket connections accepted by the server
        /// (the counter is incremented right after a successful handshake).
        /// </summary>
        public int WebSocketConnectionCount =>
            Volatile.Read(ref _webSocketCount);

        public HttpsTestServer(int port, string hostname, bool useHttp2)
        {
            _port = port;

            var certificate = CreateCertificate(hostname);

            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.Listen(
                            IPAddress.Loopback,
                            Port,
                            listenOptions =>
                            {
                                listenOptions.Protocols = useHttp2 ? HttpProtocols.Http1AndHttp2 : HttpProtocols.Http1;
                                listenOptions.UseHttps(certificate);
                                /*
                                listenOptions.UseHttps(httpsOptions => {
                                    httpsOptions.ServerCertificate = certificate;
                                    httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                                });
                                */
                            });

                        options.Limits.KeepAliveTimeout =
                            TimeSpan.FromSeconds(30);
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseWebSockets();

                        app.Use(async (context, next) =>
                        {
                            Interlocked.Increment(ref _requestCount);

                            await next();
                        });

                        app.Run(async context =>
                        {
                            switch (context.Request.Path)
                            {
                                case "/":
                                    await context.Response.WriteAsync(
                                        "Hello from HTTPS test server");
                                    break;

                                case "/one":
                                    await context.Response.WriteAsync(
                                        "Response one");
                                    break;

                                case "/two":
                                    await context.Response.WriteAsync(
                                        "Response two");
                                    break;

                                case "/three":
                                    await context.Response.WriteAsync(
                                        "Response three");
                                    break;

                                case "/echo":
                                    using (var reader =
                                        new StreamReader(context.Request.Body))
                                    {
                                        var body =
                                            await reader.ReadToEndAsync();

                                        await context.Response.WriteAsync(body);
                                    }

                                    break;

                                // ---- WebSocket endpoints ----

                                // Echoes every message back (text/binary, any size, fragmented or not).
                                case "/ws":
                                    await HandleWebSocketAsync(context, EchoAsync);
                                    break;

                                // Sends "Push 1".."Push 3" without any client request,
                                // then closes the connection with NormalClosure / "Push complete".
                                case "/ws-push":
                                    await HandleWebSocketAsync(context, PushAsync);
                                    break;

                                // Immediately closes the connection with a custom
                                // status code (4001) and description ("Closed by server").
                                case "/ws-close":
                                    await HandleWebSocketAsync(context, CloseFromServerAsync);
                                    break;

                                // Negotiates the sub-protocol: prefers "chat.v2", otherwise takes the first requested one.
                                // Then behaves like /ws.
                                case "/ws-subprotocol":
                                    {
                                        var requested = context.WebSockets.WebSocketRequestedProtocols;
                                        var selected = requested.Contains("chat.v2")
                                            ? "chat.v2"
                                            : requested.FirstOrDefault();

                                        await HandleWebSocketAsync(context, EchoAsync, selected);
                                    }
                                    break;

                                // Sends the value of the X-Test-Header request header as the first
                                // text message, then closes. Checks that the proxy forwards handshake headers.
                                case "/ws-headers":
                                    {
                                        var headerValue = context.Request.Headers["X-Test-Header"].ToString();

                                        await HandleWebSocketAsync(
                                            context,
                                            (ws, ct) => SendHeaderAsync(ws, headerValue, ct));
                                    }
                                    break;

                                default:
                                    context.Response.StatusCode = 404;

                                    await context.Response.WriteAsync(
                                        "Not found");
                                    break;
                            }
                        });
                    });
                })
                .Build();
        }

        public Task StartAsync(
            CancellationToken cancellationToken = default)
        {
            return _host.StartAsync(cancellationToken);
        }

        public Task StopAsync(
            CancellationToken cancellationToken = default)
        {
            return _host.StopAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _host.Dispose();

            return ValueTask.CompletedTask;
        }

        // ---------------------------------------------------------------
        // WebSocket handlers
        // ---------------------------------------------------------------

        private async Task HandleWebSocketAsync(
            HttpContext context,
            Func<WebSocket, CancellationToken, Task> handler,
            string? subProtocol = null)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;

                await context.Response.WriteAsync(
                    "WebSocket request expected");

                return;
            }

            using var webSocket =
                await context.WebSockets.AcceptWebSocketAsync(subProtocol);

            Interlocked.Increment(ref _webSocketCount);

            try
            {
                await handler(webSocket, context.RequestAborted);
            }
            catch (WebSocketException)
            {
                // The client dropped the connection without a close handshake - not an error for the test server.
            }
            catch (OperationCanceledException)
            {
                // The request was aborted (the host is stopping or the client has gone away).
            }
        }

        private static async Task EchoAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (webSocket.State == WebSocketState.Open)
            {
                // Collect the whole (possibly fragmented) message, then send it back as one message.
                using var message = new MemoryStream();

                ValueWebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(
                        buffer.AsMemory(), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Echo close",
                            CancellationToken.None);

                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                await webSocket.SendAsync(
                    message.ToArray(),
                    result.MessageType,
                    true,
                    cancellationToken);
            }
        }

        private static async Task PushAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            for (var i = 1; i <= 3; i++)
            {
                await webSocket.SendAsync(
                    Encoding.UTF8.GetBytes($"Push {i}"),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            }

            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Push complete",
                cancellationToken);
        }

        private static Task CloseFromServerAsync(WebSocket webSocket, CancellationToken cancellationToken)
        {
            return webSocket.CloseAsync(
                (WebSocketCloseStatus)4001,
                "Closed by server",
                cancellationToken);
        }

        private static async Task SendHeaderAsync(
            WebSocket webSocket, string headerValue, CancellationToken cancellationToken)
        {
            await webSocket.SendAsync(
                Encoding.UTF8.GetBytes(headerValue),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Header sent",
                cancellationToken);
        }

        private static X509Certificate2 CreateCertificate(string hostname)
        {
            using var rsa = RSA.Create(2048);

            var request = new CertificateRequest(
                $"CN={hostname}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    false,
                    false,
                    0,
                    true));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature,
                    true));
            /*
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    true));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },
                    true));
            */
            var san = new SubjectAlternativeNameBuilder();

            san.AddDnsName(hostname);
            san.AddIpAddress(IPAddress.Loopback);

            request.CertificateExtensions.Add(san.Build());

            // Leaf certificate signed by the test root CA (instead of self-signed),
            // so the proxy can validate the chain against the CA from SslClientOptions:TrustedCAs.
            var ca = TestRootCa.Certificate;

            // Create a unique Serial Number
            byte[] serialNumber = Guid.NewGuid().ToByteArray();
            serialNumber[^1] &= 0x7F; // keep the serial positive (big-endian: last byte is most significant in this layout, so clear it)

            using var leaf = request.Create(
                ca,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddHours(1),
                serialNumber);

            using var certificateWithKey = leaf.CopyWithPrivateKey(rsa);

            return new X509Certificate2(
                certificateWithKey.Export(X509ContentType.Pfx));
        }
    }
}
