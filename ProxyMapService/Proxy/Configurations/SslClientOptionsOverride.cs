namespace ProxyMapService.Proxy.Configurations
{
    /// <summary>
    /// Partial override of the global SslClientOptions, applied at the Listen level
    /// (per listening port range) or at the HostRule level (per target host).
    /// Only the properties explicitly set in the override replace the values
    /// inherited from the global configuration; the rest are left unchanged.
    /// Precedence: global SslClientOptions &lt; Listen.SslClientOptions &lt; HostRule.SslClientOptions.
    /// </summary>
    public class SslClientOptionsOverride
    {
        public string? EnabledSslProtocols { get; set; }
        public bool? CheckCertificateRevocation { get; set; }
        public string? ApplicationProtocols { get; set; }
        public List<TrustedCaConfig>? TrustedCAs { get; set; }
        public TlsClientType? TlsClientType { get; set; }
        public string? TlsRecordSplit { get; set; }
    }
}
