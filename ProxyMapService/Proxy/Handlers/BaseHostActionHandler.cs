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
            foreach (var rule in context.HostRules)
            {
                if (rule.HostPort == null || rule.HostPort == context.Host.Port)
                {
                    if (rule.PatternRegEx != null && rule.PatternRegEx.Match(context.Host.Hostname).Success)
                    {
                        hostAction = rule.Action;
                        hostRule = rule;
                    }
                    else if (rule.HostName != null && rule.HostName.Equals(context.Host.Hostname, StringComparison.OrdinalIgnoreCase))
                    {
                        hostAction = rule.Action;
                        hostRule = rule;
                    }
                }
            }
            context.HostAction = hostAction;
            if (hostAction != ActionEnum.Deny && hostRule != null)
            {
                if (hostRule.OverrideHostName != null)
                {
                    context.Host.OverrideHostName(hostRule.OverrideHostName);
                }
                if (hostRule.OverrideHostPort != null)
                {
                    context.Host.OverridePort(hostRule.OverrideHostPort.Value);
                }
                if (hostRule.Ssl != null)
                {
                    context.Ssl = (bool)hostRule.Ssl;
                    context.UpstreamSsl = context.Ssl;
                }
                if (hostRule.UpstreamSsl != null)
                {
                    context.UpstreamSsl = (bool)hostRule.UpstreamSsl;
                }
                if (hostRule.ServerCertificate != null)
                {
                    context.ServerCertificate = hostRule.ServerCertificate;
                }
                if (hostRule.FilesDir != null)
                {
                    context.FilesDir = hostRule.FilesDir;
                }
            }
            if (hostAction == ActionEnum.Allow && hostRule != null) 
            {                
                if (hostRule.ProxyServer != null)
                {
                    context.ProxyServer = hostRule.ProxyServer;
                }
            }
        }
    }
}
