using System.Text.RegularExpressions;

namespace ProxyMapService.Proxy.Configurations
{
    public class HostRule(string pattern, ActionEnum action, string? hostName = null, int? hostPort = null)
    {
        public Regex Pattern { get; private set; } = new Regex(pattern, RegexOptions.Compiled);
        public ActionEnum Action { get; private set; } = action;
        public string? HostName { get; private set; } = hostName;
        public int? HostPort { get; private set; } = hostPort;
    }

    public enum ActionEnum
    {
        Allow,
        Deny,
        Bypass
    }
}
