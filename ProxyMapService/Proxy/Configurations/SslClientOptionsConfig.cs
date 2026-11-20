namespace ProxyMapService.Proxy.Configurations
{
    public class SslClientOptionsConfig
    {
        public string EnabledSslProtocols { get; set; } = "Tls12,Tls13";
        public bool CheckCertificateRevocation { get; set; } = true;
        public string ApplicationProtocols { get; set; } = "http/1.1";
        public List<TrustedCaConfig> TrustedCAs { get; set; } = [];

        /// <summary>
        /// Optional splitting of the first outgoing TLS record (ClientHello) into several
        /// separate TLS records (like byedpi --split). Comma-separated split positions in the
        /// byedpi syntax "offset[:repeats:skip][+flag1[flag2]]", e.g. "1", "3+s", "0+sm", "-5".
        /// Flags: +s — SNI start, +e — end, +m — middle, +n — zero (absolute) offset.
        /// Null/empty — disabled. Applied only when the outgoing connection is TLS
        /// (see TlsClientType) and the proxy generates the ClientHello itself (DecryptSSL).
        /// </summary>
        public string? TlsRecordSplit { get; set; }

        public SslClientOptionsConfig Clone()
        {
            return new SslClientOptionsConfig
            {
                EnabledSslProtocols = EnabledSslProtocols,
                CheckCertificateRevocation = CheckCertificateRevocation,
                ApplicationProtocols = ApplicationProtocols,
                TrustedCAs = TrustedCAs,
                TlsClientType = TlsClientType,
                TlsRecordSplit = TlsRecordSplit
            };
        }

        /// <summary>
        /// Returns this config unchanged when no overrides are given, otherwise a patched
        /// clone where only the explicitly set override values differ from the original.
        /// </summary>
        public SslClientOptionsConfig ApplyOverride(SslClientOptionsOverride? overrides)
        {
            if (overrides == null)
            {
                return this;
            }
            var result = Clone();
            if (overrides.EnabledSslProtocols != null)
            {
                result.EnabledSslProtocols = overrides.EnabledSslProtocols;
            }
            if (overrides.CheckCertificateRevocation != null)
            {
                result.CheckCertificateRevocation = overrides.CheckCertificateRevocation.Value;
            }
            if (overrides.ApplicationProtocols != null)
            {
                result.ApplicationProtocols = overrides.ApplicationProtocols;
            }
            if (overrides.TrustedCAs != null)
            {
                result.TrustedCAs = overrides.TrustedCAs;
            }
            if (overrides.TlsClientType != null)
            {
                result.TlsClientType = overrides.TlsClientType.Value;
            }
            if (overrides.TlsRecordSplit != null)
            {
                result.TlsRecordSplit = overrides.TlsRecordSplit;
            }
            return result;
        }

        /// <summary>
        /// TLS stream implementation used for the upstream (outgoing) TLS connection:
        /// Native — System.Net.Security.SslStream;
        /// BoringSsl — BoringSslStream (BoringSSL-backed, Chrome-like JA4 fingerprint);
        /// Nss — NssStream (NSS-backed, Firefox-like JA4 fingerprint).
        /// </summary>
        public TlsClientType TlsClientType { get; set; } = TlsClientType.Native;
    }

    /// <summary>
    /// Upstream TLS stream implementation (ClientHello fingerprint source).
    /// </summary>
    public enum TlsClientType
    {
        /// <summary>System.Net.Security.SslStream (system TLS implementation).</summary>
        Native,
        /// <summary>BoringSslStream — BoringSSL-backed, Chrome-like JA4 fingerprint.</summary>
        BoringSsl,
        /// <summary>NssStream — NSS-backed (Mozilla), Firefox-like JA4 fingerprint.</summary>
        Nss
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
