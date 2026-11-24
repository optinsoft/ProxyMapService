namespace ProxyMapService.Proxy.Configurations
{
    /// <summary>
    /// Rules for editing HTTP headers sent to the upstream server ("RequestHeaders") and
    /// headers sent back to the client ("ResponseHeaders").
    ///
    /// Set — set/replace the given headers (an existing header with the same name is removed,
    /// the new value is appended); the value may contain "${VARIABLE}" placeholders which are
    /// substituted with environment variables (a missing variable is substituted with an empty
    /// string); CR/LF characters are stripped from the value to prevent header injection.
    /// Remove — delete the given headers. Header names are compared case-insensitively.
    ///
    /// Precedence: global (HTTP:RequestHeaders/HTTP:ResponseHeaders) &lt; Listen.RequestHeaders/
    /// Listen.ResponseHeaders &lt; HostRule.RequestHeaders/HostRule.ResponseHeaders.
    /// When the levels are merged, Set is merged by key (a higher level replaces the values of
    /// the same keys and adds new ones), the Remove lists are combined.
    /// </summary>
    public class HeadersRule
    {
        public Dictionary<string, string>? Set { get; set; }
        public List<string>? Remove { get; set; }

        public bool IsEmpty => (Set == null || Set.Count == 0) && (Remove == null || Remove.Count == 0);

        public static HeadersRule Merge(HeadersRule? baseRule, HeadersRule? overrideRule)
        {
            if (overrideRule == null || overrideRule.IsEmpty)
            {
                return baseRule ?? new HeadersRule();
            }
            if (baseRule == null || baseRule.IsEmpty)
            {
                return overrideRule;
            }

            var merged = new HeadersRule
            {
                Set = new Dictionary<string, string>(baseRule.Set ?? [], StringComparer.OrdinalIgnoreCase)
            };
            if (overrideRule.Set != null)
            {
                foreach (var (name, value) in overrideRule.Set)
                {
                    merged.Set[name] = value;
                }
            }

            var remove = new List<string>();
            if (baseRule.Remove != null)
            {
                remove.AddRange(baseRule.Remove);
            }
            if (overrideRule.Remove != null)
            {
                foreach (var name in overrideRule.Remove)
                {
                    if (!remove.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        remove.Add(name);
                    }
                }
            }
            merged.Remove = remove;
            return merged;
        }
    }
}
