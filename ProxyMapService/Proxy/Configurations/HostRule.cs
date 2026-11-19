using ProxyMapService.Proxy.Network;
using System.Text.RegularExpressions;

namespace ProxyMapService.Proxy.Configurations
{
    public class HostRule
    {
        private Regex? _patternRegEx = null;
        private string? _pattern;
        private List<CacheRule>? _hostCacheRules = null;

        public string? HostName { get; set; }
        public int? HostPort { get; set; }
        public string? Pattern 
        {
            get => _pattern;
            set
            {
                _pattern = value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    _patternRegEx = null;
                }
                else
                {
                    _patternRegEx = new Regex(value, RegexOptions.Compiled);
                }
            }
        }
        public Regex? PatternRegEx { get => _patternRegEx; }
        public ActionEnum Action { get; set; }
        public string? OverrideHostName { get; set; }
        public int? OverrideHostPort { get; set; }
        public bool? DecryptSSL { get; set; }
        public bool? IgnoreCertificateErrors { get; set; }
        public SslMode? SslMode { get; set; }
        public SslMode? UpstreamSslMode { get; set; }
        public bool? EnableTranslateH1ToH2 { get; set; }
        public bool? DisableDowngradeToHttp1ForCache { get; set; }
        public bool? EnableDowngradeToHttp1ForOverwrittenHost { get; set; }
        /// <summary>
        /// Partial override of the global SslClientOptions for connections to this host.
        /// </summary>
        public SslClientOptionsOverride? SslClientOptions { get; set; }
        /// <summary>
        /// Partial override of the global SslServerOptions (e.g. a dedicated server certificate)
        /// for connections to this host.
        /// </summary>
        public SslServerOptionsOverride? SslServerOptions { get; set; }
        public ProxyServer? ProxyServer { get; set; }
        public string? RootDir { get; set; }
        public List<CacheRule>? HostCacheRules { get => _hostCacheRules; }

        public void LoadCacheRules(CacheRules? cacheRules)
        {
            if (cacheRules != null)
            {
                _hostCacheRules = [];
                CacheRules.LoadRulesList(_hostCacheRules, cacheRules);
            }
        }

        public static HostRule? FindRule(HostAddress host, List<HostRule>? rules)
        {
            if (rules == null)
            {
                return null;
            }
            foreach (var rule in rules)
            {
                if (rule.HostPort == null || rule.HostPort == host.Port)
                {
                    if (rule.PatternRegEx != null && rule.PatternRegEx.Match(host.Hostname).Success)
                    {
                        return rule;
                    }
                    else if (rule.HostName != null && rule.HostName.Equals(host.Hostname, StringComparison.OrdinalIgnoreCase))
                    {
                        return rule;
                    }
                }
            }
            return null;
        }
    }
}
