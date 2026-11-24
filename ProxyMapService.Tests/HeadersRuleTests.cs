using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Http2;
using Xunit;

namespace ProxyMapService.Tests
{
    public class HeadersRuleTests
    {
        #region Merge

        [Fact]
        public void Merge_NullOverride_ReturnsBase()
        {
            var baseRule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["Authorization"] = "Bearer base" },
                Remove = ["Cookie"]
            };

            var merged = HeadersRule.Merge(baseRule, null);

            Assert.Equal("Bearer base", merged.Set!["Authorization"]);
            Assert.Equal(["Cookie"], merged.Remove);
        }

        [Fact]
        public void Merge_NullBase_ReturnsOverride()
        {
            var overrideRule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["X-Gateway"] = "ProxyMapService" }
            };

            var merged = HeadersRule.Merge(null, overrideRule);

            Assert.Equal("ProxyMapService", merged.Set!["X-Gateway"]);
            Assert.Null(merged.Remove);
        }

        [Fact]
        public void Merge_OverrideReplacesSameKeys_AndAddsNewOnes()
        {
            var baseRule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["Authorization"] = "Bearer base", ["X-A"] = "a" },
                Remove = ["Cookie"]
            };
            var overrideRule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["Authorization"] = "Bearer override", ["X-B"] = "b" },
                Remove = ["Proxy-Authorization"]
            };

            var merged = HeadersRule.Merge(baseRule, overrideRule);

            Assert.Equal("Bearer override", merged.Set!["Authorization"]);
            Assert.Equal("a", merged.Set["X-A"]);
            Assert.Equal("b", merged.Set["X-B"]);
            Assert.Contains("Cookie", merged.Remove!);
            Assert.Contains("Proxy-Authorization", merged.Remove!);
            Assert.Equal(2, merged.Remove!.Count);
        }

        [Fact]
        public void Merge_IsCaseInsensitiveForSetKeys()
        {
            var baseRule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["authorization"] = "Bearer base" }
            };
            var overrideRule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["AUTHORIZATION"] = "Bearer override" }
            };

            var merged = HeadersRule.Merge(baseRule, overrideRule);

            Assert.Single(merged.Set!);
            Assert.Equal("Bearer override", merged.Set!["Authorization"]);
        }

        #endregion

        #region Apply (HTTP/1.1 lines)

        [Fact]
        public void Apply_EmptyRule_DoesNothing()
        {
            var lines = new List<string> { "GET / HTTP/1.1", "Host: example.com" };

            Assert.False(HttpHeaderRewriter.ApplyHeadersRule(new HeadersRule(), lines));
            Assert.Equal(["GET / HTTP/1.1", "Host: example.com"], lines);
        }

        [Fact]
        public void Apply_AddsNewHeader_BeforeTrailingEmptyLines()
        {
            var rule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["X-Gateway"] = "ProxyMapService" }
            };
            var lines = new List<string> { "GET / HTTP/1.1", "Host: example.com", "", "" };

            Assert.True(HttpHeaderRewriter.ApplyHeadersRule(rule, lines));
            Assert.Equal(["GET / HTTP/1.1", "Host: example.com", "X-Gateway: ProxyMapService", "", ""], lines);
        }

        [Fact]
        public void Apply_ReplacesExistingHeader_KeepingPosition()
        {
            var rule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["Authorization"] = "Bearer ${TEST_TOKEN}" }
            };
            var lines = new List<string> { "GET / HTTP/1.1", "Host: example.com", "Authorization: Basic abc" };

            Environment.SetEnvironmentVariable("TEST_TOKEN", "secret");
            try
            {
                Assert.True(HttpHeaderRewriter.ApplyHeadersRule(rule, lines));
                Assert.Equal(["GET / HTTP/1.1", "Host: example.com", "Authorization: Bearer secret"], lines);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TEST_TOKEN", null);
            }
        }

        [Fact]
        public void Apply_RemovesAllOccurrences_CaseInsensitive()
        {
            var rule = new HeadersRule { Remove = ["cookie", "Proxy-Authorization"] };
            var lines = new List<string>
            {
                "GET / HTTP/1.1",
                "Cookie: a=1",
                "Host: example.com",
                "cookie: b=2",
                "Proxy-Authorization: Basic xyz"
            };

            Assert.True(HttpHeaderRewriter.ApplyHeadersRule(rule, lines));
            Assert.Equal(["GET / HTTP/1.1", "Host: example.com"], lines);
        }

        [Fact]
        public void Apply_NeverTouchesRequestLine()
        {
            var rule = new HeadersRule { Remove = ["Host"] };

            var lines = new List<string> { "Host: example.com", "Host: other.com" };

            HttpHeaderRewriter.ApplyHeadersRule(rule, lines);
            Assert.Equal(["Host: example.com"], lines);
        }

        [Fact]
        public void Apply_StripsCrlf_FromValues()
        {
            var rule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["X-Evil"] = "value\r\nX-Injected: yes" }
            };
            var lines = new List<string> { "GET / HTTP/1.1" };

            HttpHeaderRewriter.ApplyHeadersRule(rule, lines);
            Assert.Equal(["GET / HTTP/1.1", "X-Evil: valueX-Injected: yes"], lines);
        }

        [Fact]
        public void Apply_MissingEnvironmentVariable_BecomesEmpty()
        {
            var rule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["X-Token"] = "Bearer ${HEADERS_RULE_TESTS_DEFINITELY_NOT_SET}" }
            };
            var lines = new List<string> { "GET / HTTP/1.1" };

            HttpHeaderRewriter.ApplyHeadersRule(rule, lines);
            // the whole resolved value is trimmed, hence no trailing space after "Bearer"
            Assert.Equal(["GET / HTTP/1.1", "X-Token: Bearer"], lines);
        }

        [Fact]
        public void Apply_MultiplePlaceholders_AreSubstituted()
        {
            var rule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["X-Combo"] = "${T1}-${T2}" }
            };
            var lines = new List<string> { "GET / HTTP/1.1" };

            Environment.SetEnvironmentVariable("T1", "aaa");
            Environment.SetEnvironmentVariable("T2", "bbb");
            try
            {
                HttpHeaderRewriter.ApplyHeadersRule(rule, lines);
                Assert.Equal(["GET / HTTP/1.1", "X-Combo: aaa-bbb"], lines);
            }
            finally
            {
                Environment.SetEnvironmentVariable("T1", null);
                Environment.SetEnvironmentVariable("T2", null);
            }
        }

        #endregion

        #region Apply (HTTP/2 headers)

        [Fact]
        public void ApplyH2_SetsAndRemovesRegularHeaders_KeepsPseudoHeaders()
        {
            var rule = new HeadersRule
            {
                Set = new Dictionary<string, string> { ["x-gateway"] = "ProxyMapService" },
                Remove = ["User-Agent"]
            };
            var headers = new List<H2Header>
            {
                new(":method", "GET"),
                new(":authority", "example.com"),
                new("user-agent", "TestAgent")
            };

            Assert.True(HttpHeaderRewriter.ApplyHeadersRule(rule, headers));
            Assert.Equal(
            [
                new H2Header(":method", "GET"),
                new H2Header(":authority", "example.com"),
                new H2Header("x-gateway", "ProxyMapService")
            ], headers);
        }

        [Fact]
        public void ApplyH2_IgnoresPseudoHeaderNames_InSetAndRemove()
        {
            var rule = new HeadersRule
            {
                Set = new Dictionary<string, string> { [":method"] = "POST" },
                Remove = [":authority"]
            };
            var headers = new List<H2Header>
            {
                new(":method", "GET"),
                new(":authority", "example.com")
            };

            Assert.False(HttpHeaderRewriter.ApplyHeadersRule(rule, headers));
            Assert.Equal(
            [
                new H2Header(":method", "GET"),
                new H2Header(":authority", "example.com")
            ], headers);
        }

        #endregion
    }
}
