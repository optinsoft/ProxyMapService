using System.Text.RegularExpressions;

namespace ProxyMapService.Proxy.Configurations
{
    public class HostRule(string pattern, ActionEnum action)
    {
        public Regex Pattern { get; init; } = new Regex(pattern, RegexOptions.Compiled);
        public ActionEnum Action { get; init; } = action;
        public string? OverrideHostName { get; set; }
        public int? OverrideHostPort { get; set; }
        public ProxyServer? ProxyServer { get; set; }
    }

    public enum ActionEnum
    {
        Allow,
        Deny,
        Bypass
    }
}
