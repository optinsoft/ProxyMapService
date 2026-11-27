using Microsoft.Extensions.Logging.Abstractions;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// Sticky-session client User-Agent semantics in SessionManager:
    /// explicit values (Session API) are never overwritten by passive values taken from
    /// HTTP request headers; passive values are replaced by newer passive values;
    /// the value is cleared by ClearCurrentUserAgent (session reset / sticky expiry).
    /// </summary>
    public class SessionManagerUserAgentTests
    {
        private readonly SessionManager _manager;

        public SessionManagerUserAgentTests()
        {
            _manager = new SessionManager(NullLogger.Instance);
        }

        [Fact]
        public void SetExplicit_GetReturnsExplicit()
        {
            _manager.SetCurrentUserAgent("Mozilla/5.0 ... Firefox/132.0", explicitSet: true);
            var (userAgent, explicitSet) = _manager.GetCurrentUserAgent();
            Assert.Equal("Mozilla/5.0 ... Firefox/132.0", userAgent);
            Assert.True(explicitSet);
        }

        [Fact]
        public void SetFromHeaders_GetReturnsNotExplicit()
        {
            _manager.SetCurrentUserAgent("curl/8.11.1", explicitSet: false);
            var (userAgent, explicitSet) = _manager.GetCurrentUserAgent();
            Assert.Equal("curl/8.11.1", userAgent);
            Assert.False(explicitSet);
        }

        [Fact]
        public void ExplicitValue_IsNotOverwrittenByPassiveValue()
        {
            _manager.SetCurrentUserAgent("Mozilla/5.0 ... Chrome/131.0", explicitSet: true);
            _manager.SetCurrentUserAgent("curl/8.11.1", explicitSet: false);

            var (userAgent, explicitSet) = _manager.GetCurrentUserAgent();
            Assert.Equal("Mozilla/5.0 ... Chrome/131.0", userAgent);
            Assert.True(explicitSet);
        }

        [Fact]
        public void ExplicitValue_IsReplacedByAnotherExplicitValue()
        {
            _manager.SetCurrentUserAgent("Mozilla/5.0 ... Chrome/131.0", explicitSet: true);
            _manager.SetCurrentUserAgent("Mozilla/5.0 ... Firefox/132.0", explicitSet: true);

            var (userAgent, explicitSet) = _manager.GetCurrentUserAgent();
            Assert.Equal("Mozilla/5.0 ... Firefox/132.0", userAgent);
            Assert.True(explicitSet);
        }

        [Fact]
        public void PassiveValue_IsReplacedByNewerPassiveValue()
        {
            _manager.SetCurrentUserAgent("old-header-ua/1.0", explicitSet: false);
            _manager.SetCurrentUserAgent("new-header-ua/2.0", explicitSet: false);

            var (userAgent, explicitSet) = _manager.GetCurrentUserAgent();
            Assert.Equal("new-header-ua/2.0", userAgent);
            Assert.False(explicitSet);
        }

        [Fact]
        public void SetNull_Passive_DoesNotClearExplicitValue()
        {
            _manager.SetCurrentUserAgent("Mozilla/5.0 ... Chrome/131.0", explicitSet: true);
            _manager.SetCurrentUserAgent(null, explicitSet: false);

            var (userAgent, explicitSet) = _manager.GetCurrentUserAgent();
            Assert.Equal("Mozilla/5.0 ... Chrome/131.0", userAgent);
            Assert.True(explicitSet);
        }

        [Fact]
        public void ResetSessionId_ShouldResetUserAgent()
        {
            // ResetSessionId (session rotation via /session/new) must drop the User-Agent
            _manager.SetCurrentUserAgent("Mozilla/5.0 ... Chrome/131.0", explicitSet: true);
            _manager.ResetSessionId();
            var (userAgent, explicitSet) = _manager.GetCurrentUserAgent();
            Assert.Null(userAgent);
            Assert.False(explicitSet);
        }

        [Fact]
        public void InitialState_IsEmpty()
        {
            var (userAgent, explicitSet) = _manager.GetCurrentUserAgent();
            Assert.Null(userAgent);
            Assert.False(explicitSet);
        }
    }
}
