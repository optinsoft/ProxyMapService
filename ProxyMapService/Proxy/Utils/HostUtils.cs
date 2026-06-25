using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Utils
{
    public static class HostUtils
    {
        public static void GetContextHostAction(SessionContext context, bool httpMode)
        {
            if (IsSessionAPIHost(context.SessionAPI, context.Host))
            {
                context.HostAction = SessionActionEnum.SessionAPI;
                return;
            }
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
                if (hostRule.DecryptSSL != null)
                {
                    context.DecryptSSL = hostRule.DecryptSSL.Value;
                }
                if (hostRule.SslMode != null)
                {
                    if (!httpMode)
                    {
                        context.SslMode = hostRule.SslMode.Value;
                    }
                }
                if (hostRule.UpstreamSslMode != null)
                {
                    context.UpstreamSslMode = hostRule.UpstreamSslMode.Value;
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

        public static bool IsSessionAPIHost(SessionAPIConfig config, HostAddress? host)
        {
            if (!config.Enabled)
            {
                return false;
            }
            if (string.IsNullOrEmpty(config.Domain))
            {
                return host == null || host.Hostname.Length == 0;
            }
            else
            {
                return config.Domain.Equals(host?.Hostname);
            }
        }
    }
}
