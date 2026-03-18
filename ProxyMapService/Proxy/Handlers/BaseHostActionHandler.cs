using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseHostActionHandler
    {
        protected static void GetContextHostAction(SessionContext context)
        {
            HostRule? hostRule = HostRule.FindRule(context.Host, context.HostRules);
            ActionEnum hostAction = hostRule?.Action ?? ActionEnum.Allow;
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
                if (hostRule.RootDir != null)
                {
                    context.RootDir = hostRule.RootDir;
                }
                if (hostRule.HostCacheRules != null)
                {
                    context.CacheRules = hostRule.HostCacheRules;
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
