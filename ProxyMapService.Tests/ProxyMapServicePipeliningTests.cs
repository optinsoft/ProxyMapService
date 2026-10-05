using Microsoft.Extensions.Configuration;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// Tests the situation when two HTTP/1.1 requests arrive at TunnelHandler
    /// in a single source.ReadAsync call (pipelining).
    /// </summary>
    public class ProxyMapServicePipeliningTests
    {
        [Fact]
        public async Task Http11_Proxy_Https_TwoPipelinedGets_InSingleRead_ShouldWork()
        {
            await using var server = new HttpsTestServer(5112, useHttp2: false);
            await server.StartAsync();

            var authority = $"localhost:{server.Port}";

            var raw =
                $"GET /one HTTP/1.1\r\nHost: {authority}\r\n\r\n" +
                $"GET /two HTTP/1.1\r\nHost: {authority}\r\nConnection: close\r\n\r\n";

            var response = await SendPipelinedAsync(authority, Encoding.ASCII.GetBytes(raw));

            Assert.Equal(2, CountOccurrences(response, "HTTP/1.1 200"));
            Assert.Contains("Response one", response);
            Assert.Contains("Response two", response);
            // The order of responses must match the order of requests.
            Assert.True(response.IndexOf("Response one", StringComparison.Ordinal)
                      < response.IndexOf("Response two", StringComparison.Ordinal));
        }

        [Fact]
        public async Task Http11_Proxy_Https_PostWithBodyThenGet_InSingleRead_ShouldWork()
        {
            await using var server = new HttpsTestServer(5113, useHttp2: false);
            await server.StartAsync();

            var authority = $"localhost:{server.Port}";
            const string body = "Hello through ProxyMapService";

            var raw =
                $"POST /echo HTTP/1.1\r\nHost: {authority}\r\nContent-Length: {body.Length}\r\n\r\n{body}" +
                $"GET /two HTTP/1.1\r\nHost: {authority}\r\nConnection: close\r\n\r\n";

            var response = await SendPipelinedAsync(authority, Encoding.ASCII.GetBytes(raw));

            Assert.Equal(2, CountOccurrences(response, "HTTP/1.1 200"));
            Assert.Contains(body, response);
            Assert.Contains("Response two", response);
        }

        // ---------------------------------------------------------------

        private static async Task<string> SendPipelinedAsync(string authority, byte[] pipelinedRequests)
        {
            // The entire packet must fit into the TunnelHandler buffer (BufferSize = 8192),
            // otherwise it is guaranteed to be read in two chunks.
            Assert.True(pipelinedRequests.Length < 8192);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var proxyHost = configuration["Proxy:Host"] ?? "127.0.0.1";
            var proxyPort = configuration.GetValue<int>("Proxy:Port", 5001);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
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
                TargetHost = "localhost",
                RemoteCertificateValidationCallback = (_, _, _, _) => true // self-signed
            }, token);

            // 3. Both requests in a single Write -> a single TLS record -> a single ReadAsync on the proxy side
            await ssl.WriteAsync(pipelinedRequests, token);
            await ssl.FlushAsync(token);

            // 4. Read everything until close (the second request has Connection: close)
            using var ms = new MemoryStream();
            try
            {
                await ssl.CopyToAsync(ms, token);
            }
            catch (IOException)
            {
                // Closing without close_notify is acceptable; we take whatever was read.
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        // Read byte by byte to avoid "eating" extra data after \r\n\r\n.
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

        private static int CountOccurrences(string text, string value)
        {
            int count = 0, index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }
    }
}
