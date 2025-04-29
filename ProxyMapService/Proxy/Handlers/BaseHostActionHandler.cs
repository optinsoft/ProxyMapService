using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseHostActionHandler
    {
        protected static ActionEnum GetHostAction(string host, List<HostRule>? hostRuleList, out HostRule? hostRule)
        {
            hostRule = null;
            ActionEnum hostAction = ActionEnum.Allow;
            if (hostRuleList != null)
            {
                foreach (var rule in hostRuleList)
                {
                    if (rule.Pattern.Match(host).Success)
                    {
                        hostAction = rule.Action;
                        hostRule = rule;
                    }
                }
            }
            return hostAction;
        }
    }
}
