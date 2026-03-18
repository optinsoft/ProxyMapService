using System.Text.RegularExpressions;

namespace ProxyMapService.Proxy.Configurations
{
    public class CacheRule
    {
        private Regex? _patternRegEx = null;
        private string? _pattern;

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

        public static CacheRule? FindRule(string? url, List<CacheRule>? rules)
        {
            if (url == null || rules == null)
            {
                return null;
            }
            foreach (var rule in rules)
            {
                if (rule.PatternRegEx != null && rule.PatternRegEx.Match(url).Success)
                {
                    return rule;
                }
            }
            return null;
        }
    }
}
