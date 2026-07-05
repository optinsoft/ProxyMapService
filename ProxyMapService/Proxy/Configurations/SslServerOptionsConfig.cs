using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Configurations
{
    public class SslServerOptionsConfig : IDisposable
    {
        private readonly SslCertificateConfig _serverCertificate = new();
        private readonly SslCertificateConfig _caCertificate = new();

        public string EnabledSslProtocols { get; set; } = "Tls12,Tls13";
        public bool ClientCertificateRequired { get; set; } = false;
        public bool CheckCertificateRevocation { get; set; } = true;
        public SslCertificateConfig ServerCertificate { get => _serverCertificate; }
        public SslCertificateConfig CACertificate { get => _caCertificate; }

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