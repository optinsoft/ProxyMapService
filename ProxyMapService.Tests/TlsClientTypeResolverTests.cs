using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// TlsClientType.Auto resolution from the client (browser) User-Agent:
    /// Firefox → Nss, Chrome-based → BoringSsl, everything else → Native.
    /// </summary>
    public class TlsClientTypeResolverTests
    {
        private static readonly string[] ChromeUserAgentList =
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
            // Edge and Opera are Chromium-based and report "Chrome/" as well
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.2903.86",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 OPR/115.0.0.0"
        };

        public static IEnumerable<object[]> ChromeUserAgents =>
            ChromeUserAgentList.Select(ua => new object[] { ua });

        private static readonly string[] FirefoxUserAgentList =
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14.7; rv:133.0) Gecko/20100101 Firefox/133.0",
            "Mozilla/5.0 (X11; Linux x86_64; rv:128.0) Gecko/20100101 Firefox/128.0"
        };

        public static IEnumerable<object[]> FirefoxUserAgents =>
            FirefoxUserAgentList.Select(ua => new object[] { ua });

        private static readonly string[] NativeUserAgentList =
        {
            // Safari (macOS)
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.6 Safari/605.1.15",
            // Safari (iOS)
            "Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1",
            // Command-line clients and bots
            "curl/8.11.1",
            "Python/3.12 aiohttp/3.10.10",
            "Go-http-client/2.0",
            "Wget/1.21.4"
        };

        public static IEnumerable<object[]> NativeUserAgents =>
            NativeUserAgentList.Select(ua => new object[] { ua });

        [Theory]
        [MemberData(nameof(ChromeUserAgents))]
        public void ChromeFamily_ResolvesToBoringSsl(string userAgent)
        {
            Assert.Equal(TlsClientType.BoringSsl, TlsClientTypeResolver.Resolve(userAgent));
        }

        [Theory]
        [MemberData(nameof(FirefoxUserAgents))]
        public void Firefox_ResolvesToNss(string userAgent)
        {
            Assert.Equal(TlsClientType.Nss, TlsClientTypeResolver.Resolve(userAgent));
        }

        [Theory]
        [MemberData(nameof(NativeUserAgents))]
        public void OtherClients_ResolvesToNative(string userAgent)
        {
            Assert.Equal(TlsClientType.Native, TlsClientTypeResolver.Resolve(userAgent));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MissingUserAgent_ResolvesToNative(string? userAgent)
        {
            Assert.Equal(TlsClientType.Native, TlsClientTypeResolver.Resolve(userAgent));
        }

        [Fact]
        public void FirefoxIsCheckedBeforeChrome_UaSpoofingIsNotFooled()
        {
            // A Firefox UA that embeds "Chrome" (e.g. via extensions or gateways)
            // must still resolve to Nss.
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Chrome/99.0 Gecko/20100101 Firefox/132.0";
            Assert.Equal(TlsClientType.Nss, TlsClientTypeResolver.Resolve(userAgent));
        }
    }
}
