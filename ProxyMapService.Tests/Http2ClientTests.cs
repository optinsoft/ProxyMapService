using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace ProxyMapService.Tests
{
    public class Http2ClientTests
    {
        [Fact]
        public async Task Http2_Client_Proxy_Https_Get_ShouldWork()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";

            await using var server = new HttpsTestServer(5114, hostname: testHost, useHttp2: true);
            await server.StartAsync();

            var connections = 0;
            using var client = CreateHttp2ClientThroughProxy(configuration, () => Interlocked.Increment(ref connections));

            using var response = await SendHttp2Async(client, HttpMethod.Get, $"https://{testHost}:{server.Port}/one");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(HttpVersion.Version20, response.Version);
            Assert.Contains("Response one", await response.Content.ReadAsStringAsync());
            Assert.Equal(1, connections);
        }

        [Fact]
        public async Task Http2_Client_Proxy_Https_ParallelRequests_ShouldMultiplexOverSingleTunnel()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";

            await using var server = new HttpsTestServer(5115, hostname: testHost, useHttp2: true);
            await server.StartAsync();

            var connections = 0;
            using var client = CreateHttp2ClientThroughProxy(configuration, () => Interlocked.Increment(ref connections));

            var baseUrl = $"https://{testHost}:{server.Port}";

            using (var warmup = await SendHttp2Async(client, HttpMethod.Get, $"{baseUrl}/one"))
            {
                Assert.Equal(HttpVersion.Version20, warmup.Version);
                await warmup.Content.ReadAsStringAsync();
            }

            var tasks = Enumerable.Range(0, 10).Select(async i =>
            {
                var path = i % 2 == 0 ? "/one" : "/two";
                var expected = i % 2 == 0 ? "Response one" : "Response two";

                using var response = await SendHttp2Async(client, HttpMethod.Get, baseUrl + path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal(HttpVersion.Version20, response.Version);
                Assert.Contains(expected, await response.Content.ReadAsStringAsync());
            }).ToArray();

            await Task.WhenAll(tasks);

            Assert.Equal(1, connections);
        }

        [Fact]
        public async Task Http2_Client_Proxy_Https_PostWithBodyThenGet_ShouldWork()
        {
            var configuration = BuildConfiguration();
            var testHost = configuration.GetValue<string>("TestServer:Host") ?? "localhost";

            await using var server = new HttpsTestServer(5116, hostname: testHost, useHttp2: true);
            await server.StartAsync();

            var connections = 0;
            using var client = CreateHttp2ClientThroughProxy(configuration, () => Interlocked.Increment(ref connections));

            var baseUrl = $"https://{testHost}:{server.Port}";
            const string body = "Hello through ProxyMapService";

            using var post = await SendHttp2Async(client, HttpMethod.Post, $"{baseUrl}/echo",
                new StringContent(body, Encoding.ASCII, "text/plain"));
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            Assert.Equal(HttpVersion.Version20, post.Version);
            Assert.Contains(body, await post.Content.ReadAsStringAsync());

            using var get = await SendHttp2Async(client, HttpMethod.Get, $"{baseUrl}/two");
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            Assert.Equal(HttpVersion.Version20, get.Version);
            Assert.Contains("Response two", await get.Content.ReadAsStringAsync());

            Assert.Equal(1, connections);
        }

        // ---------------------------------------------------------------

        private static IConfigurationRoot BuildConfiguration() =>
            new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

        private static async Task<HttpResponseMessage> SendHttp2Async(
            HttpClient client, HttpMethod method, string url, HttpContent? content = null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var request = new HttpRequestMessage(method, url)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Content = content
            };

            return await client.SendAsync(request, cts.Token);
        }

        private static HttpClient CreateHttp2ClientThroughProxy(IConfigurationRoot configuration, Action onTunnelOpened)
        {
            var proxyHost = configuration["Proxy:Host"] ?? "127.0.0.1";
            var proxyPort = configuration.GetValue<int>("Proxy:Port", 5001);

            var handler = new SocketsHttpHandler
            {
                UseProxy = false, // туннель строим сами
                ConnectCallback = async (context, token) =>
                {
                    onTunnelOpened();

                    var authority = $"{context.DnsEndPoint.Host}:{context.DnsEndPoint.Port}";

                    var tcp = new TcpClient { NoDelay = true };
                    try
                    {
                        await tcp.ConnectAsync(proxyHost, proxyPort, token);
                        var network = tcp.GetStream();

                        var connect = Encoding.ASCII.GetBytes(
                            $"CONNECT {authority} HTTP/1.1\r\nHost: {authority}\r\n\r\n");
                        await network.WriteAsync(connect, token);

                        var connectResponse = await ReadHeadersAsync(network, token);
                        Assert.StartsWith("HTTP/1.1 200", connectResponse);

                        // Возвращаем "сырой" туннель: TLS + ALPN поверх него сделает сам SocketsHttpHandler
                        return network;
                    }
                    catch
                    {
                        tcp.Dispose();
                        throw;
                    }
                },
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (_, _, _, _) => true // self-signed
                }
            };

            return new HttpClient(handler, disposeHandler: true);
        }

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
