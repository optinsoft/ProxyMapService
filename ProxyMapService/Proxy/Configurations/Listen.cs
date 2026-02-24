using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Configurations
{
    public class Listen
    {
        private PortRange _portRange = new();
        private string? _certificatePath = null;
        private string? _certificatePassword = null;
        private X509Certificate2? _serverCertificate = null;
        private bool _certificateinitialized = false;

        public int Port {
            get
            {
                return _portRange.Start;
            }
            set
            {
                _portRange.Start = value;
                _portRange.End = value;
            }
        }

        public PortRange PortRange {
            get
            {
                return _portRange;
            }
            set
            {
                _portRange = value;
            }
        }

        public bool RejectHttpProxy { get; set; }
        public int StickyProxyLifetime {  get; set; }
        public bool Ssl { get; set; }
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
