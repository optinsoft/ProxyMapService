using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Configurations
{
    public class SslServerOptionsConfig
    {
        private string? _certificatePath = null;
        private string? _certificatePassword = null;
        private X509Certificate2? _serverCertificate = null;
        private bool _certificateinitialized = false;
        public string EnabledSslProtocols { get; set; } = "Tls12,Tls13";
        public bool ClientCertificateRequired { get; set; } = false;
        public bool CheckCertificateRevocation { get; set; } = true;
        public string? CertificatePath
        {
            get => _certificatePath;
            set
            {
                _certificatePath = value;
                _certificateinitialized = false;
            }
        }
        public string? CertificatePassword
        {
            get => _certificatePassword;
            set
            {
                _certificatePassword = value;
                _certificateinitialized = false;
            }
        }
        public X509Certificate2? ServerCertificate
        {
            get
            {
                if (!_certificateinitialized)
                {
                    if (_certificatePath != null)
                    {
                        _serverCertificate = new X509Certificate2(
                            _certificatePath,
                            _certificatePassword);
                    }
                    _certificateinitialized = true;
                }
                return _serverCertificate;
            }
        }
    }
}
