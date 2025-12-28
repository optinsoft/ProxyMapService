using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;
using System.Data;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseHostActionHandler
    {
        protected static void GetContextHostAction(SessionContext context)
        {
            HostRule? hostRule = null;
            ActionEnum hostAction = ActionEnum.Allow;
            if (context.HostRules != null)
            {
                foreach (var rule in context.HostRules)
                {
                    if (rule.Pattern.Match(context.HostName).Success)
                    {
                        hostAction = rule.Action;
                        hostRule = rule;
                    }
                }
            }
            context.HostAction = hostAction;
            if (hostAction != ActionEnum.Deny)
            {
                if (hostRule?.OverrideHostName != null)
                {
                    context.HostName = hostRule.OverrideHostName;
                }
                if (hostRule?.OverrideHostPort != null)
                {
                    context.HostPort = hostRule.OverrideHostPort.Value;
                }
            }
            if (hostAction == ActionEnum.Allow) 
            {                
                if (hostRule?.ProxyServer != null)
                {
                    context.ProxyServer = hostRule.ProxyServer;
                }
            }
        }
    }
}
