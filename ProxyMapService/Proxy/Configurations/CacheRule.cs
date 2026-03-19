using System.Text.RegularExpressions;

namespace ProxyMapService.Proxy.Configurations
{
    public class CacheRule
    {
        private Regex? _patternRegEx = null;
        private string? _pattern;
        private Regex? _acceptPatternRegEx = null;
        private string? _acceptPattern;

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
        public string? AcceptPattern
        {
            get => _acceptPattern;
            set
            {
                _acceptPattern = value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    _acceptPatternRegEx = null;
                }
                else
                {
                    _acceptPatternRegEx = new Regex(value, RegexOptions.Compiled);
                }
            }
        }
        public Regex? AcceptPatternRegEx { get => _acceptPatternRegEx; }

        public static CacheRule? FindRule(string? url, string? accept, List<CacheRule>? rules)
        {
            if (url == null || rules == null)
            {
                return null;
            }
            foreach (var rule in rules)
            {
                bool matched = false;
                if (rule.PatternRegEx != null && rule.PatternRegEx.Match(url).Success)
                {
                    matched = true;
                }
                if (matched)
                {
                    if (rule.AcceptPatternRegEx != null && !rule.AcceptPatternRegEx.Match(accept ?? "").Success)
                    {
                        matched = false;
                    }
                }
                if (matched)
                {
                    return rule;
                }
            }
            return null;
        }
    }
}
