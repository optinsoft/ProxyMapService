namespace ProxyMapService.Proxy.Configurations
{
    public class SslClientOptionsConfig
    {
        public string EnabledSslProtocols { get; set; } = "Tls12,Tls13";
        public bool CheckCertificateRevocation { get; set; } = true;
        public string ApplicationProtocols { get; set; } = "http/1.1";
        public List<TrustedCaConfig> TrustedCAs { get; set; } = [];

        /// <summary>
        /// Use BoringSslStream (BoringSSL-backed TLS stream with Chrome-like JA4 fingerprints)
        /// instead of System.Net.Security.SslStream for the upstream (outgoing) TLS connection.
        /// </summary>
        public bool UseBoringSsl { get; set; } = false;
    }

    /// <summary>
    /// A root CA whose certificates are accepted as trusted for upstream (server-side) TLS validation.
    /// The certificate is resolved either from a file (PEM/DER) or from a local X509Store.
    /// </summary>
    public class TrustedCaConfig
    {
        public string? PemFile { get; set; }
        public string? CerFile { get; set; }
        public string? StoreLocation { get; set; }
        public string? StoreName { get; set; }
        public string? Subject { get; set; }
    }
}
