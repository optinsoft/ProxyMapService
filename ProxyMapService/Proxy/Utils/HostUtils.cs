using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Utils
{
    public static class HostUtils
    {
        public static void SetContextHostAction(SessionContext context, bool httpMode)
        {
            HostRule? hostRule = context.IgnoreHostRules ? null : HostRule.FindRule(context.Host, context.HostRules);
            SessionAction hostAction = hostRule?.Action switch
            {
                ActionEnum.Allow => context.Action == SessionActionEnum.Bypass ? SessionActionEnum.Bypass : SessionActionEnum.Proxy,
                ActionEnum.Deny => SessionActionEnum.Deny,
                ActionEnum.Bypass => SessionActionEnum.Bypass,
                ActionEnum.File => SessionActionEnum.File,
                ActionEnum.Proxy => SessionActionEnum.Proxy,
                _ => context.Action
            };
            context.HostAction = hostAction;
            if (hostAction != SessionActionEnum.Deny && hostRule != null)
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
                if (hostRule.TlsRecordSplit != null)
                {
                    context.TlsRecordSplit = hostRule.TlsRecordSplit;
                }
                if (hostRule.IgnoreCertificateErrors != null)
                {
                    context.IgnoreCertificateErrors = hostRule.IgnoreCertificateErrors.Value;
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
                if (hostRule.EnableTranslateH1ToH2 != null)
                {
                    context.H1ToH2TranslationEnabled = hostRule.EnableTranslateH1ToH2.Value;
                }
                if (hostRule.DisableDowngradeToHttp1ForCache != null)
                {
                    context.DowngradeToHttp1ForCacheEnabled = !hostRule.DisableDowngradeToHttp1ForCache.Value;
                }
                if (hostRule.EnableDowngradeToHttp1ForOverwrittenHost != null)
                {
                    context.DowngradeToHttp1ForOverwrittenHostEnabled = hostRule.EnableDowngradeToHttp1ForOverwrittenHost.Value;
                }
                if (hostRule.RequestHeaders != null)
                {
                    context.RequestHeadersRule = HeadersRule.Merge(context.RequestHeadersRule, hostRule.RequestHeaders);
                }
                if (hostRule.ResponseHeaders != null)
                {
                    context.ResponseHeadersRule = HeadersRule.Merge(context.ResponseHeadersRule, hostRule.ResponseHeaders);
                }
                if (hostRule.SslClientOptions != null)
                {
                    context.SslClientConfig = context.SslClientConfig.ApplyOverride(hostRule.SslClientOptions);
                }
                if (hostRule.SslServerOptions != null)
                {
                    context.SslServerConfig = context.SslServerConfig.ApplyOverride(hostRule.SslServerOptions);
                    try
                    {
                        var overriddenServerCertificate = context.SslServerConfig.ServerCertificate.Certificate;
                        if (overriddenServerCertificate != null)
                        {
                            context.ServerCertificate = overriddenServerCertificate;
                        }
                        var overriddenCaCertificate = context.SslServerConfig.CACertificate.Certificate;
                        if (overriddenCaCertificate != null)
                        {
                            context.CACertificate = overriddenCaCertificate;
                        }
                    }
                    catch (Exception ex)
                    {
                        context.Logger.LogError("Failed to load host rule SSL certificate overrides: {ErrorMessage}", ex.Message);
                    }
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
            if (hostAction == SessionActionEnum.Proxy && hostRule != null)
            {
                if (hostRule.ProxyServer != null)
                {
                    context.ProxyServer = hostRule.ProxyServer;
                }
            }
            if (hostAction != SessionActionEnum.Deny)
            {
                context.SessionManager.PopulateContext(context);
            }
        }

        public static bool IsSessionAPIHost(SessionContext context)
        {
            if (!context.SessionAPI.Enabled)
            {
                return false;
            }
            if (string.IsNullOrEmpty(context.SessionAPI.Domain))
            {
                return false;
            }
            var host = context.Http?.Host != null
                ? HostAddress.GetHostAddress(context.Http.Host, false)
                : context.Host;
            return context.SessionAPI.Domain.Equals(host?.Hostname);
        }
    }
}
