using Microsoft.Extensions.Logging.Abstractions;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// Sticky-session client User-Agent semantics in SessionManager:
    /// an explicit value survives session restarts, a header-derived value is replaced
    /// on every session, and everything is dropped by ResetSessionId.
    /// </summary>
    public class SessionManagerUserAgentTests
    {
        private static readonly SessionManager Manager = new(NullLogger.Instance);

        private static (string? UserAgent, bool Explicit) RestartWithoutExplicit() =>
            Manager.GetCurrentUserAgent();

        [Fact]
        public void SetExplicit_GetReturnsExplicit()
        {
            Manager.SetCurrentUserAgent("Mozilla/5.0 ... Firefox/132.0", explicitSet: true);
            var (userAgent, explicitSet) = Manager.GetCurrentUserAgent();
            Assert.Equal("Mozilla/5.0 ... Firefox/132.0", userAgent);
            Assert.True(explicitSet);
        }

        [Fact]
        public void SetFromHeaders_GetReturnsNotExplicit()
        {
            Manager.SetCurrentUserAgent("curl/8.11.1", explicitSet: false);
            var (userAgent, explicitSet) = Manager.GetCurrentUserAgent();
            Assert.Equal("curl/8.11.1", userAgent);
            Assert.False(explicitSet);
        }

        [Fact]
        public void SetNull_ClearsValue()
        {
            Manager.SetCurrentUserAgent("curl/8.11.1", explicitSet: true);
            Manager.SetCurrentUserAgent(null, explicitSet: false);
            var (userAgent, explicitSet) = Manager.GetCurrentUserAgent();
            Assert.Null(userAgent);
            Assert.False(explicitSet);
        }

        [Fact]
        public void ExplicitValue_SurvivesSimulatedSessionRestart()
        {
            // Simulates the SessionAPIHandler.NewSession flow: read current state,
            // ResetSessionId (which clears), re-apply without an explicit parameter.
            Manager.SetCurrentUserAgent("Mozilla/5.0 ... Chrome/131.0", explicitSet: true);

            var (previousUserAgent, previousExplicit) = RestartWithoutExplicit();
            Manager.ResetSessionId();

            // Re-apply: explicit value is restored, header value is ignored.
            if (previousExplicit)
            {
                Manager.SetCurrentUserAgent(previousUserAgent, explicitSet: true);
            }
            else
            {
                Manager.SetCurrentUserAgent("new-header-ua/1.0", explicitSet: false);
            }

            var (userAgent, explicitSet) = Manager.GetCurrentUserAgent();
            Assert.Equal("Mozilla/5.0 ... Chrome/131.0", userAgent);
            Assert.True(explicitSet);
        }

        [Fact]
        public void HeaderValue_IsReplacedOnSimulatedSessionRestart()
        {
            Manager.SetCurrentUserAgent("old-header-ua/1.0", explicitSet: false);

            var (_, previousExplicit) = RestartWithoutExplicit();
            Manager.ResetSessionId();

            Assert.False(previousExplicit);
            Manager.SetCurrentUserAgent("new-header-ua/2.0", explicitSet: false);

            var (userAgent, _) = Manager.GetCurrentUserAgent();
            Assert.Equal("new-header-ua/2.0", userAgent);
        }

        [Fact]
        public void ResetSessionId_ClearsUserAgent()
        {
            Manager.SetCurrentUserAgent("Mozilla/5.0 ... Chrome/131.0", explicitSet: true);
            Manager.ResetSessionId();
            var (userAgent, explicitSet) = Manager.GetCurrentUserAgent();
            Assert.Null(userAgent);
            Assert.False(explicitSet);
        }

        [Fact]
        public void InitialState_IsEmpty()
        {
            var manager = new SessionManager(NullLogger.Instance);
            var (userAgent, explicitSet) = manager.GetCurrentUserAgent();
            Assert.Null(userAgent);
            Assert.False(explicitSet);
        }
    }
}
