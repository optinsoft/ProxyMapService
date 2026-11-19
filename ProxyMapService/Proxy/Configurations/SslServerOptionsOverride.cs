using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Configurations
{
    /// <summary>
    /// Partial override of the global SslServerOptions, applied at the Listen level
    /// (per listening port range) or at the HostRule level (per target host).
    /// Only the properties explicitly set in the override replace the values
    /// inherited from the global configuration; the rest are left unchanged.
    /// Precedence: global SslServerOptions &lt; Listen.SslServerOptions &lt; HostRule.SslServerOptions.
    /// </summary>
    public class SslServerOptionsOverride
    {
        public string? EnabledSslProtocols { get; set; }
        public bool? ClientCertificateRequired { get; set; }
        public bool? CheckCertificateRevocation { get; set; }
        public string? ApplicationProtocols { get; set; }
        public SslCertificateConfig? ServerCertificate { get; set; }
        public SslCertificateConfig? CACertificate { get; set; }
    }
}
