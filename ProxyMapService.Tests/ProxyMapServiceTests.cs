using System.Net;
using System.Net.Http;
using Xunit;

namespace ProxyMapService.Tests
{
    public class ProxyMapServiceTests
    {
        [Fact]
        public async Task Http11_Proxy_Https_MultipleRequests_ShouldWork()
        {
            await using var server = new HttpsTestServer();

            await server.StartAsync();

            var proxy = new WebProxy("http://127.0.0.1:5010");

            var handler = new HttpClientHandler
            {
                Proxy = proxy,
                UseProxy = true,

                // Self-signed certificate is expected.
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            using var client = new HttpClient(handler);

            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

            var baseUrl = $"https://localhost:{HttpsTestServer.Port}";

            // Request #1
            var response1 = await client.GetAsync($"{baseUrl}/one");

            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

            var body1 = await response1.Content.ReadAsStringAsync();

            Assert.Equal("Response one", body1);

            // Request #2
            var response2 = await client.GetAsync($"{baseUrl}/two");

            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

            var body2 = await response2.Content.ReadAsStringAsync();

            Assert.Equal("Response two", body2);

            // Request #3
            var response3 = await client.GetAsync($"{baseUrl}/");

            Assert.Equal(HttpStatusCode.OK, response3.StatusCode);

            var body3 = await response3.Content.ReadAsStringAsync();

            Assert.Equal(
                "Hello from HTTPS test server",
                body3);

            // Request #4 with request body
            var response4 = await client.PostAsync(
                $"{baseUrl}/echo",
                new StringContent("Hello through ProxyMapService"));

            Assert.Equal(HttpStatusCode.OK, response4.StatusCode);

            var body4 = await response4.Content.ReadAsStringAsync();

            Assert.Equal(
                "Hello through ProxyMapService",
                body4);
        }
    }
}
