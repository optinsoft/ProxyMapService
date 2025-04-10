using System.Text.RegularExpressions;

namespace ProxyMapService.Proxy.Configurations
{
    public class HostRule(string pattern, ActionEnum action)
    {
        public Regex Pattern { get; private set; } = new Regex(pattern, RegexOptions.Compiled);
        public ActionEnum Action { get; private set; } = action;
    }

    public enum ActionEnum
    {
        Allow,
        Deny,
        Bypass
    }
}
