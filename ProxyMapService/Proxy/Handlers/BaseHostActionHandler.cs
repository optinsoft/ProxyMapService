using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseHostActionHandler
    {
        protected static ActionEnum GetHostAction(string Host, List<HostRule>? hostRules)
        {
            ActionEnum hostAction = ActionEnum.Allow;
            if (hostRules != null)
            {
                foreach (var rule in hostRules)
                {
                    if (rule.Pattern.Match(Host).Success)
                    {
                        hostAction = rule.Action;
                    }
                }
            }
            return hostAction;
        }
    }
}
