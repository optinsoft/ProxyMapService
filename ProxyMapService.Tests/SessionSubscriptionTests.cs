using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Xunit;
using Xunit.Sdk;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// Integration tests for the Session API WebSocket (/session/ws): subscribe / unsubscribe / url_matched events.
    ///
    /// Prerequisites (the running ProxyMapService must be configured so that):
    ///  - the mapping used by these tests is DEDICATED to them: no other tests (and no other traffic) may go
    ///    through it. The proxy keeps ONE current session per mapping and delivers url_matched events for
    ///    every request of that session, so foreign requests would leak into the subscriptions and
    ///    /session/new from here would rotate the session under foreign tests;
    ///  - the proxy for these tests is configured with SessionAPI:Proxy:Host / SessionAPI:Proxy:Port
    ///    (fallback: Proxy:Host / Proxy:Port, which is only safe if no other tests use that mapping);
    ///  - SessionAPI is enabled with WebSocketsEnabled = true and is reachable on SessionAPI:Host / SessionAPI:Port
    ///    (fallback: the proxy host and port); SessionAPI:Domain is the host name used in the API URLs
    ///    (fallback: SessionAPI:Host);
    ///  - the mapping supports sessions, i.e. GET /session/new returns a non-null X-Session-Id and
    ///    requests going through the proxy afterwards resolve to that same session id;
    ///  - HTTPS traffic through the proxy is decrypted (the proxy has to see the request URL).
    ///
    /// Every test starts with /session/new, so the tests of this class must not run in parallel with each
    /// other: they share the [Collection("SessionApi")]. Any other test class that uses the same mapping
    /// must join that collection as well.
    /// </summary>
    [Collection("SessionApi")]
    public class SessionSubscriptionTests
    {
        private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan SilencePeriod = TimeSpan.FromMilliseconds(700);

        // ---------------------------------------------------------------
        // subscribe
        // ---------------------------------------------------------------

        [Fact]
        public async Task Subscribe_ReturnsSubIdAndCurrentSessionId()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            var sub = await SubscribeAsync(ws, ".*");

            Assert.Equal(sessionId, sub.SessionId);
            Assert.False(string.IsNullOrEmpty(sub.SubId));
        }

        [Fact]
        public async Task Subscribe_WithoutPattern_MatchesEverything()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            await ws.SendTextAsync("{\"action\":\"subscribe\"}");
            var reply = await ws.NextAsync();
            Assert.Equal("subscribed", reply.GetProperty("status").GetString());

            await env.RequestAsync(HttpMethod.Get, "/one");

            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/one"));
        }

        [Fact]
        public async Task ProxiedRequest_DeliversUrlMatchedEvent()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            await SubscribeAsync(ws, ".*");

            await env.RequestAsync(HttpMethod.Get, "/one");

            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/one"));
        }

        [Fact]
        public async Task ProxiedRequest_WithQueryString_ReportsUrlVerbatim()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            await SubscribeAsync(ws, ".*");

            await env.RequestAsync(HttpMethod.Get, "/one?x=1&y=2");

            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/one?x=1&y=2"));
        }

        [Fact]
        public async Task ProxiedRequest_ReportsHttpMethod()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            await SubscribeAsync(ws, ".*");

            await env.RequestAsync(HttpMethod.Post, "/echo", "payload");

            AssertUrlMatched(await ws.NextAsync(), sessionId, "POST", env.Url("/echo"));
        }

        // ---------------------------------------------------------------
        // pattern filtering
        // ---------------------------------------------------------------

        [Fact]
        public async Task Pattern_FiltersEvents()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            await SubscribeAsync(ws, "/two$");

            await env.RequestAsync(HttpMethod.Get, "/one");
            await env.RequestAsync(HttpMethod.Get, "/two");

            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/two"));
            await ws.ExpectNoMessageAsync();
        }

        [Fact]
        public async Task Pattern_IsCaseInsensitive()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            await SubscribeAsync(ws, "/ONE$");

            await env.RequestAsync(HttpMethod.Get, "/one");

            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/one"));
        }

        [Fact]
        public async Task InvalidPattern_ReturnsError_AndConnectionStaysUsable()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();

            await ws.SendJsonAsync(new { action = "subscribe", pattern = "(" });
            Assert.Equal("invalid_pattern", (await ws.NextAsync()).GetProperty("error").GetString());

            await SubscribeAsync(ws, ".*");
            await env.RequestAsync(HttpMethod.Get, "/one");

            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/one"));
        }

        // ---------------------------------------------------------------
        // unsubscribe
        // ---------------------------------------------------------------

        [Fact]
        public async Task Unsubscribe_StopsEvents()
        {
            await using var env = await TestEnv.StartAsync();
            await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            var sub = await SubscribeAsync(ws, ".*");

            await ws.SendJsonAsync(new { action = "unsubscribe", sub_id = sub.SubId });
            var reply = await ws.NextAsync();
            Assert.Equal("unsubscribed", reply.GetProperty("status").GetString());
            Assert.Equal(sub.SubId, reply.GetProperty("sub_id").GetString());

            await env.RequestAsync(HttpMethod.Get, "/one");

            await ws.ExpectNoMessageAsync();
        }

        [Fact]
        public async Task Unsubscribe_UnknownSubId_ReturnsError()
        {
            await using var env = await TestEnv.StartAsync();
            await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();

            await ws.SendJsonAsync(new { action = "unsubscribe", sub_id = "no-such-id" });
            var reply = await ws.NextAsync();

            Assert.Equal("unknown_sub_id", reply.GetProperty("error").GetString());
            Assert.Equal("no-such-id", reply.GetProperty("sub_id").GetString());
        }

        [Theory]
        [InlineData("{\"action\":\"unsubscribe\"}")]
        [InlineData("{\"action\":\"unsubscribe\",\"sub_id\":123}")]
        [InlineData("{\"action\":\"unsubscribe\",\"sub_id\":null}")]
        public async Task Unsubscribe_MissingOrNonStringSubId_ReturnsError(string raw)
        {
            await using var env = await TestEnv.StartAsync();
            await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();

            await ws.SendTextAsync(raw);

            Assert.Equal("missing_sub_id", (await ws.NextAsync()).GetProperty("error").GetString());
        }

        [Fact]
        public async Task Unsubscribe_ForeignSubId_IsRejected_AndOwnerKeepsReceiving()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var owner = await env.ConnectWsAsync();
            await using var intruder = await env.ConnectWsAsync();
            var ownerSub = await SubscribeAsync(owner, ".*");

            await intruder.SendJsonAsync(new { action = "unsubscribe", sub_id = ownerSub.SubId });
            Assert.Equal("unknown_sub_id", (await intruder.NextAsync()).GetProperty("error").GetString());

            await env.RequestAsync(HttpMethod.Get, "/one");

            AssertUrlMatched(await owner.NextAsync(), sessionId, "GET", env.Url("/one"));
        }

        // ---------------------------------------------------------------
        // several subscriptions / several connections
        // ---------------------------------------------------------------

        [Fact]
        public async Task TwoSubscriptions_OnOneConnection_UnsubscribingOneKeepsTheOther()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            var subOne = await SubscribeAsync(ws, "/one$");
            var subTwo = await SubscribeAsync(ws, "/two$");
            Assert.NotEqual(subOne.SubId, subTwo.SubId);

            await ws.SendJsonAsync(new { action = "unsubscribe", sub_id = subOne.SubId });
            Assert.Equal("unsubscribed", (await ws.NextAsync()).GetProperty("status").GetString());

            await env.RequestAsync(HttpMethod.Get, "/one");
            await ws.ExpectNoMessageAsync();

            await env.RequestAsync(HttpMethod.Get, "/two");
            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/two"));
        }

        [Fact]
        public async Task TwoSubscriptions_MatchingSameUrl_DeliverTwoEvents()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            await SubscribeAsync(ws, "/one$");
            await SubscribeAsync(ws, "one");

            await env.RequestAsync(HttpMethod.Get, "/one");

            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/one"));
            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/one"));
            await ws.ExpectNoMessageAsync();
        }

        [Fact]
        public async Task TooManySubscriptions_ReturnsError()
        {
            const int limit = 50; // SessionManager.MaxSubscriptionsPerConnection

            await using var env = await TestEnv.StartAsync();
            await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();
            for (var i = 0; i < limit; i++)
            {
                await SubscribeAsync(ws, $"/path{i}$");
            }

            await ws.SendJsonAsync(new { action = "subscribe", pattern = ".*" });

            Assert.Equal("too_many_subscriptions", (await ws.NextAsync()).GetProperty("error").GetString());
        }

        [Fact]
        public async Task TwoConnections_SameSession_BothReceive_AndClosingOneDoesNotAffectTheOther()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            var first = await env.ConnectWsAsync();
            await using var second = await env.ConnectWsAsync();
            await SubscribeAsync(first, ".*");
            await SubscribeAsync(second, ".*");

            await env.RequestAsync(HttpMethod.Get, "/one");
            AssertUrlMatched(await first.NextAsync(), sessionId, "GET", env.Url("/one"));
            AssertUrlMatched(await second.NextAsync(), sessionId, "GET", env.Url("/one"));

            await first.DisposeAsync(); // drops the connection without a close handshake

            await env.RequestAsync(HttpMethod.Get, "/two");
            AssertUrlMatched(await second.NextAsync(), sessionId, "GET", env.Url("/two"));
        }

        [Fact]
        public async Task Subscription_IsBoundToSessionAtConnectTime()
        {
            await using var env = await TestEnv.StartAsync();
            var firstSession = await env.NewSessionAsync();

            await using var oldWs = await env.ConnectWsAsync();
            var oldSub = await SubscribeAsync(oldWs, ".*");
            Assert.Equal(firstSession, oldSub.SessionId);

            // Rotate the session. Do NOT send proxied requests before this point:
            // a kept-alive tunnel could keep the old session id.
            var secondSession = await env.NewSessionAsync();
            Assert.NotEqual(firstSession, secondSession);

            await env.RequestAsync(HttpMethod.Get, "/one");

            // The old connection is still subscribed to the old session -> nothing for the new one.
            await oldWs.ExpectNoMessageAsync();

            // A new connection resolves the new session and receives events for it.
            await using var newWs = await env.ConnectWsAsync();
            var newSub = await SubscribeAsync(newWs, ".*");
            Assert.Equal(secondSession, newSub.SessionId);

            await env.RequestAsync(HttpMethod.Get, "/two");
            AssertUrlMatched(await newWs.NextAsync(), secondSession, "GET", env.Url("/two"));
        }

        // ---------------------------------------------------------------
        // protocol robustness
        // ---------------------------------------------------------------

        [Theory]
        [InlineData("not json", "invalid_json")]
        [InlineData("{", "invalid_json")]
        [InlineData("{}", "missing_action")]
        [InlineData("[]", "missing_action")]
        [InlineData("{\"action\":123}", "missing_action")]
        [InlineData("{\"action\":\"nope\"}", "unknown_action")]
        public async Task MalformedCommand_ReturnsErrorCode_AndKeepsConnectionOpen(string raw, string expectedError)
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();

            await ws.SendTextAsync(raw);
            Assert.Equal(expectedError, (await ws.NextAsync()).GetProperty("error").GetString());

            // the connection is still alive and usable
            var sub = await SubscribeAsync(ws, ".*");
            Assert.Equal(sessionId, sub.SessionId);
        }

        [Fact]
        public async Task FragmentedMessage_IsReassembled()
        {
            await using var env = await TestEnv.StartAsync();
            var sessionId = await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();

            var bytes = Encoding.UTF8.GetBytes("{\"action\":\"subscribe\",\"pattern\":\"/one$\"}");
            await ws.SendFragmentAsync(bytes[..15], last: false);
            await ws.SendFragmentAsync(bytes[15..], last: true);

            var reply = await ws.NextAsync();
            Assert.Equal("subscribed", reply.GetProperty("status").GetString());

            await env.RequestAsync(HttpMethod.Get, "/one");
            AssertUrlMatched(await ws.NextAsync(), sessionId, "GET", env.Url("/one"));
        }

        [Fact]
        public async Task BinaryMessage_ClosesConnection_WithInvalidMessageType()
        {
            await using var env = await TestEnv.StartAsync();
            await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();

            await ws.SendBinaryAsync(new byte[] { 1, 2, 3 });
            await ws.WaitClosedAsync();

            Assert.Equal(WebSocketCloseStatus.InvalidMessageType, ws.CloseStatus);
        }

        [Fact]
        public async Task OversizedMessage_ClosesConnection_WithMessageTooBig()
        {
            await using var env = await TestEnv.StartAsync();
            await env.NewSessionAsync();

            await using var ws = await env.ConnectWsAsync();

            // MaxMessageSize is 64 KB
            await ws.SendTextAsync(new string('a', 70_000));
            await ws.WaitClosedAsync();

            Assert.Equal(WebSocketCloseStatus.MessageTooBig, ws.CloseStatus);
        }

        // ---------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------

        private sealed record Subscribed(string SubId, string SessionId);

        private static async Task<Subscribed> SubscribeAsync(WsClient ws, string pattern)
        {
            await ws.SendJsonAsync(new { action = "subscribe", pattern });
            var reply = await ws.NextAsync();

            Assert.Equal("subscribed", reply.GetProperty("status").GetString());
            return new Subscribed(
                reply.GetProperty("sub_id").GetString()!,
                reply.GetProperty("session_id").GetString()!);
        }

        private static void AssertUrlMatched(JsonElement e, string sessionId, string method, string url)
        {
            Assert.Equal("url_matched", e.GetProperty("event_type").GetString());
            Assert.Equal(sessionId, e.GetProperty("session_id").GetString());
            Assert.Equal(method, e.GetProperty("method").GetString());
            Assert.Equal(url, e.GetProperty("url").GetString());
            Assert.True(e.TryGetProperty("timestamp", out _));
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>Proxy + HTTPS test server + HTTP clients for one test.</summary>
        private sealed class TestEnv : IAsyncDisposable
        {
            private readonly HttpsTestServer _server;
            private readonly HttpClient _direct;     // talks to the Session API directly
            private readonly HttpClient _viaProxy;   // goes through the proxy to the test server
            private readonly string _apiHost;
            private readonly string _apiDomain;
            private readonly int _apiPort;
            private readonly string _testHost;

            private TestEnv(HttpsTestServer server, string proxyHost, int proxyPort,
                string apiHost, string apiDomain, int apiPort, string testHost)
            {
                _server = server;
                _apiHost = apiHost;
                _apiDomain = apiDomain;
                _apiPort = apiPort;
                _testHost = testHost;

                var directHandler = new SocketsHttpHandler
                {
                    ConnectCallback = async (context, cancellationToken) =>
                    {
                        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        try
                        {
                            await socket.ConnectAsync(_apiHost, context.DnsEndPoint.Port, cancellationToken);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    },
                    UseProxy = false
                };

                _direct = new HttpClient(directHandler);

                var proxyHandler = new HttpClientHandler
                {
                    Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}"),
                    UseProxy = true,
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                _viaProxy = new HttpClient(proxyHandler)
                {
                    DefaultRequestVersion = HttpVersion.Version11,
                    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
                };
            }

            public static async Task<TestEnv> StartAsync()
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                var proxyHost = configuration["SessionAPI:Proxy:Host"] ?? configuration["Proxy:Host"] ?? "127.0.0.1";
                var proxyPort = configuration.GetValue<int>("SessionAPI:Proxy:Port", configuration.GetValue<int>("Proxy:Port", 5001));
                var apiHost = configuration["SessionAPI:Host"] ?? proxyHost;
                var apiDomain = configuration["SessionAPI:Domain"] ?? apiHost;
                var apiPort = configuration.GetValue<int>("SessionAPI:Port", proxyPort);
                var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";
                var certHost = configuration.GetValue<string>("TestServer:CertHost") ?? testHost;
                var useHttp2 = configuration.GetValue<bool>("TestServer:UseHttp2", false);

                var server = new HttpsTestServer(GetFreePort(), hostname: certHost, useHttp2: useHttp2);
                await server.StartAsync();

                return new TestEnv(server, proxyHost, proxyPort, apiHost, apiDomain, apiPort, testHost);
            }

            /// <summary>URL of the test server resource as the proxy is expected to report it.</summary>
            public string Url(string pathAndQuery) => $"https://{_testHost}:{_server.Port}{pathAndQuery}";

            /// <summary>GET /session/new: starts a fresh session and returns its id.</summary>
            public async Task<string> NewSessionAsync()
            {
                using var response = await _direct.GetAsync($"http://{_apiDomain}:{_apiPort}/session/new");

                response.EnsureSuccessStatusCode();

                Assert.True(response.Headers.TryGetValues("X-Session-Id", out var values),
                    "X-Session-Id header is missing in /session/new response");

                var id = values!.Single();
                Assert.False(string.IsNullOrEmpty(id) || id == "null",
                    "/session/new returned no session id: the mapping must have sessions enabled");
                return id;
            }

            public Task<WsClient> ConnectWsAsync()
            {
                var handler = new SocketsHttpHandler
                {
                    ConnectCallback = async (context, cancellationToken) =>
                    {
                        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        try
                        {
                            await socket.ConnectAsync(_apiHost, context.DnsEndPoint.Port, cancellationToken);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    }
                };
                return WsClient.ConnectAsync(new Uri($"ws://{_apiDomain}:{_apiPort}/session/ws"), handler);
            }

            /// <summary>Sends a request through the proxy to the HTTPS test server and expects 200.</summary>
            public async Task RequestAsync(HttpMethod method, string pathAndQuery, string? body = null)
            {
                using var request = new HttpRequestMessage(method, Url(pathAndQuery));
                if (body != null)
                {
                    request.Content = new StringContent(body);
                }

                using var response = await _viaProxy.SendAsync(request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            public async ValueTask DisposeAsync()
            {
                _direct.Dispose();
                _viaProxy.Dispose();
                await _server.DisposeAsync();
            }
        }

        /// <summary>
        /// WebSocket client that reads messages in the background into a channel, so that tests can
        /// both wait for a message and assert that NO message arrives (cancelling a pending
        /// ClientWebSocket.ReceiveAsync would abort the socket).
        /// </summary>
        private sealed class WsClient(SocketsHttpHandler? handler) : IAsyncDisposable
        {
            private readonly ClientWebSocket _ws = new();
            private readonly Channel<JsonElement> _messages = Channel.CreateUnbounded<JsonElement>();
            private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly CancellationTokenSource _cts = new();
            private Task _pump = Task.CompletedTask;
            private readonly HttpMessageInvoker? _invoker = handler != null ? new HttpMessageInvoker(handler) : null;

            public WebSocketCloseStatus? CloseStatus => _ws.CloseStatus;

            public static async Task<WsClient> ConnectAsync(Uri uri, SocketsHttpHandler? handler)
            {
                var client = new WsClient(handler);
                client._ws.Options.Proxy = null; // never go through a system proxy

                using var cts = new CancellationTokenSource(ReceiveTimeout);
                await client._ws.ConnectAsync(uri, client._invoker, cts.Token);

                client._pump = Task.Run(client.PumpAsync);
                return client;
            }

            private async Task PumpAsync()
            {
                var buffer = new byte[4096];
                try
                {
                    while (_ws.State == WebSocketState.Open)
                    {
                        using var ms = new MemoryStream();
                        ValueWebSocketReceiveResult r;
                        do
                        {
                            r = await _ws.ReceiveAsync(buffer.AsMemory(), _cts.Token);
                            if (r.MessageType == WebSocketMessageType.Close)
                            {
                                // complete the close handshake so the server's CloseAsync can finish
                                try
                                {
                                    await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack", CancellationToken.None);
                                }
                                catch (Exception)
                                {
                                }
                                return;
                            }
                            ms.Write(buffer, 0, r.Count);
                        }
                        while (!r.EndOfMessage);

                        using var doc = JsonDocument.Parse(ms.GetBuffer().AsMemory(0, (int)ms.Length));
                        _messages.Writer.TryWrite(doc.RootElement.Clone());
                    }
                }
                catch (Exception)
                {
                    // connection dropped or client disposed
                }
                finally
                {
                    _messages.Writer.TryComplete();
                    _closed.TrySetResult();
                }
            }

            public Task SendJsonAsync(object payload) => SendTextAsync(JsonSerializer.Serialize(payload));

            public Task SendTextAsync(string text) =>
                _ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, _cts.Token);

            public Task SendBinaryAsync(byte[] data) =>
                _ws.SendAsync(data, WebSocketMessageType.Binary, true, _cts.Token);

            public Task SendFragmentAsync(byte[] data, bool last) =>
                _ws.SendAsync(data, WebSocketMessageType.Text, last, _cts.Token);

            public async Task<JsonElement> NextAsync()
            {
                using var cts = new CancellationTokenSource(ReceiveTimeout);
                try
                {
                    return await _messages.Reader.ReadAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"No WebSocket message within {ReceiveTimeout}");
                }
                catch (ChannelClosedException)
                {
                    throw new XunitException("WebSocket was closed before the expected message arrived");
                }
            }

            public async Task ExpectNoMessageAsync()
            {
                using var cts = new CancellationTokenSource(SilencePeriod);
                try
                {
                    if (await _messages.Reader.WaitToReadAsync(cts.Token)
                        && _messages.Reader.TryPeek(out var unexpected))
                    {
                        throw new XunitException($"Unexpected WebSocket message: {unexpected}");
                    }
                }
                catch (OperationCanceledException)
                {
                    // silence, as expected
                }
            }

            public async Task WaitClosedAsync()
            {
                var finished = await Task.WhenAny(_closed.Task, Task.Delay(ReceiveTimeout));
                Assert.Same(_closed.Task, finished);
            }

            public async ValueTask DisposeAsync()
            {
                _cts.Cancel();
                _ws.Abort();
                _ws.Dispose();
                try
                {
                    await _pump;
                }
                catch (Exception)
                {
                }
                _cts.Dispose();
                if (_invoker != null)
                {
                    _invoker.Dispose();
                }
            }
        }
    }
}
