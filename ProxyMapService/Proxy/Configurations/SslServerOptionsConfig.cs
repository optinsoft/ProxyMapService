using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Configurations
{
    public class SslServerOptionsConfig
    {
        private SslCertificateConfig _serverCertificate = new();
        private SslCertificateConfig _caCertificate = new();

        public string EnabledSslProtocols { get; set; } = "Tls12,Tls13";
        public bool ClientCertificateRequired { get; set; } = false;
        public bool CheckCertificateRevocation { get; set; } = true;
        public SslCertificateConfig ServerCertificate { get => _serverCertificate; }
        public SslCertificateConfig CACertificate {  get => _caCertificate; }        
    }
}
