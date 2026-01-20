using System.Text.RegularExpressions;

namespace ProxyMapService.Proxy.Configurations
{
    public class HostRule
    {
        private Regex? _patternRegEx = null;
        private string? _pattern;
        public string? HostName { get; set; }
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
        public ProxyServer? ProxyServer { get; set; }
        public string? FilesDir { get; set; }
    }

    public enum ActionEnum
    {
        Allow,
        Deny,
        Bypass,
        File
    }
}
