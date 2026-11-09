using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// Captures the TLS ClientHello that the proxy sends to an upstream HTTPS
    /// server and computes its JA4 fingerprint.
    ///
    /// With SslClientOptions:UseBoringSsl = true the ClientHello must be
    /// Chrome-like (the BoringSslStream cipher-suite hash matches Google
    /// Chrome's); with UseBoringSsl = false (System.Net.Security.SslStream)
    /// it must differ.
    ///
    /// Requires the test proxy instance to be running
    /// (start_proxymapservice_test.sh / .ps1).
    /// </summary>
    public class Ja4FingerprintTests
    {
        private readonly ITestOutputHelper _output;

        public Ja4FingerprintTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Chrome's JA4 cipher-suite hash (see FoxIO-LLC/ja4): independent of
        // ALPN/ALPS, so it identifies the Chrome-like ClientHello produced by
        // BoringSslStream.
        private const string ChromeCipherHash = "8daaf6152771";

        [Fact]
        public async Task Upstream_TLS_ClientHello_JA4_Fingerprint()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var proxyHost = configuration["Proxy:Host"] ?? "127.0.0.1";
            var proxyPort = configuration.GetValue<int>("Proxy:Port", 5001);

            bool useBoringSsl = ReadUseBoringSsl();
            _output.WriteLine($"SslClientOptions:UseBoringSsl = {useBoringSsl}");

            await using var capture = new ClientHelloCaptureServer();
            _output.WriteLine($"ClientHello capture server: 127.0.0.1:{capture.Port}");

            using var client = new HttpClient(new HttpClientHandler
            {
                Proxy = new WebProxy($"http://{proxyHost}:{proxyPort}"),
                UseProxy = true,

                // The capture server closes the connection right after reading
                // the ClientHello, so the TLS handshake never completes.
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            client.Timeout = TimeSpan.FromSeconds(15);

            try
            {
                await client.GetAsync($"https://localhost:{capture.Port}/one");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Expected: the capture server drops the connection before the
                // TLS handshake completes.
                _output.WriteLine($"Request failed as expected: {ex.GetType().Name}: {ex.Message}");
            }

            var hello = await capture.FirstHelloAsync(TimeSpan.FromSeconds(15));

            var ja4 = Ja4Fingerprint.Compute(hello, out var rawCiphers, out var rawExtensions);

            _output.WriteLine("");
            _output.WriteLine($"JA4:   {ja4}");
            _output.WriteLine($"JA4_r: t..._{rawCiphers}_{rawExtensions}");
            _output.WriteLine("");
            _output.WriteLine("Cipher suites:");
            foreach (var cipher in rawCiphers.Split(','))
            {
                _output.WriteLine($"    {cipher}");
            }
            _output.WriteLine("Extensions:");
            foreach (var extension in rawExtensions.Split(','))
            {
                _output.WriteLine($"    {extension}");
            }
            _output.WriteLine("");

            Assert.NotNull(ja4);

            // TLS 1.3 offered, SNI present.
            Assert.StartsWith("t13d", ja4);

            if (useBoringSsl)
            {
                // BoringSslStream replicates Google Chrome's ClientHello:
                // the cipher-suite hash must match Chrome's JA4.
                Assert.True(
                    ja4.Contains(ChromeCipherHash),
                    $"Expected Chrome-like JA4 with UseBoringSsl=true, got: {ja4}");
            }
            else
            {
                // Plain SslStream must NOT produce Chrome's ClientHello.
                Assert.True(
                    !ja4.Contains(ChromeCipherHash),
                    $"SslStream unexpectedly produced Chrome-like JA4: {ja4}");
            }
        }

        /// <summary>
        /// Reads SslClientOptions:UseBoringSsl from the proxy's appsettings.Test.json
        /// (the configuration the test proxy instance is started with).
        /// </summary>
        private static bool ReadUseBoringSsl()
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "ProxyMapService", "appsettings.Test.json");

            if (!File.Exists(path))
            {
                return false;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("SslClientOptions", out var sslOptions) &&
                   sslOptions.TryGetProperty("UseBoringSsl", out var useBoringSsl) &&
                   useBoringSsl.ValueKind == JsonValueKind.True;
        }
    }
}
