using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Configurations
{
    public class SslServerOptionsConfig : IDisposable
    {
        private SslCertificateConfig _serverCertificate = new();
        private SslCertificateConfig _caCertificate = new();

        public string EnabledSslProtocols { get; set; } = "Tls12,Tls13";
        public bool ClientCertificateRequired { get; set; } = false;
        public bool CheckCertificateRevocation { get; set; } = true;
        public string ApplicationProtocols { get; set; } = "http/1.1";
        public SslCertificateConfig ServerCertificate { get => _serverCertificate; }
        public SslCertificateConfig CACertificate { get => _caCertificate; }

        // Used by ConfigurationTopLayerBinder to replace a whole certificate section
        // (bound from a single configuration layer) instead of merging layers key by key.
        internal void ReplaceServerCertificate(SslCertificateConfig certificate)
        {
            _serverCertificate = certificate;
        }

        internal void ReplaceCACertificate(SslCertificateConfig certificate)
        {
            _caCertificate = certificate;
        }

        public SslServerOptionsConfig Clone()
        {
            // The lazily-loaded certificate configs are shared with the original;
            // they are replaced only when an override specifies its own certificates.
            return new SslServerOptionsConfig
            {
                EnabledSslProtocols = EnabledSslProtocols,
                ClientCertificateRequired = ClientCertificateRequired,
                CheckCertificateRevocation = CheckCertificateRevocation,
                ApplicationProtocols = ApplicationProtocols,
                _serverCertificate = _serverCertificate,
                _caCertificate = _caCertificate
            };
        }

        /// <summary>
        /// Returns this config unchanged when no overrides are given, otherwise a patched
        /// clone where only the explicitly set override values differ from the original.
        /// </summary>
        public SslServerOptionsConfig ApplyOverride(SslServerOptionsOverride? overrides)
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
            if (overrides.ClientCertificateRequired != null)
            {
                result.ClientCertificateRequired = overrides.ClientCertificateRequired.Value;
            }
            if (overrides.CheckCertificateRevocation != null)
            {
                result.CheckCertificateRevocation = overrides.CheckCertificateRevocation.Value;
            }
            if (overrides.ApplicationProtocols != null)
            {
                result.ApplicationProtocols = overrides.ApplicationProtocols;
            }
            if (overrides.ServerCertificate != null)
            {
                result._serverCertificate = overrides.ServerCertificate;
            }
            if (overrides.CACertificate != null)
            {
                result._caCertificate = overrides.CACertificate;
            }
            return result;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _serverCertificate.Dispose();
                _caCertificate.Dispose();
            }
        }
    }
}
