using System.Text.RegularExpressions;

namespace ProxyMapService.Proxy.Configurations
{
    public class CacheRule
    {
        private Regex? _patternRegEx = null;
        private string? _pattern;
        private Regex? _acceptPatternRegEx = null;
        private string? _acceptPattern;
        private Regex? _contentTypePatternRegEx = null;
        private string? _contentTypePattern;

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

        public string? ContentTypePattern
        {
            get => _contentTypePattern;
            set
            {
                _contentTypePattern = value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    _contentTypePatternRegEx = null;
                }
                else
                {
                    _contentTypePatternRegEx = new Regex(value, RegexOptions.Compiled);
                }
            }
        }
        public Regex? ContentTypePatternRegEx { get => _contentTypePatternRegEx; }

        public int MaxAge { get; set; }
        public bool IgnoreCacheControl {  get; set; }

        public static List<CacheRule> FindRules(string? url, string? accept, List<CacheRule>? rules)
        {
            List<CacheRule> result = [];
            if (url == null || rules == null)
            {
                return result;
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
                    result.Add(rule);
                }
            }
            return result;
        }

        public static CacheRule? FindCacheContentTypeRule(string? contentType, List<CacheRule>? rules)
        {
            if (rules == null || rules.Count == 0)
            {
                return null;
            }
            CacheRule? result = null;
            foreach (var rule in rules)
            {
                if (rule.ContentTypePatternRegEx != null)
                {
                    if (rule.ContentTypePatternRegEx.Match(contentType ?? "").Success)
                    {
                        return rule;
                    }
                }
                else
                {
                    result ??= rule;
                }
            }
            return result;
        }
    }
}
