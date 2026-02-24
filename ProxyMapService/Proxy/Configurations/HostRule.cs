using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace ProxyMapService.Proxy.Configurations
{
    public class HostRule
    {
        private Regex? _patternRegEx = null;
        private string? _pattern;
        private string? _certificatePath = null;
        private string? _certificatePassword = null;
        private X509Certificate2? _serverCertificate = null;
        private bool _certificateinitialized = false;
        public string? HostName { get; set; }
        public int? HostPort { get; set; }
        public string? Pattern 
        {
            get => _pattern;
            set
            {
                _pattern = value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    _patternRegEx = null;
                }
                else
                {
                    _patternRegEx = new Regex(value, RegexOptions.Compiled);
                }
            }
        }
        public Regex? PatternRegEx { get => _patternRegEx; }
        public ActionEnum Action { get; set; }
        public string? OverrideHostName { get; set; }
        public int? OverrideHostPort { get; set; }
        public bool? Ssl { get; set; }
        public string? CertificatePath { 
            get => _certificatePath; 
            set
            {
                _certificatePath = value;
                _certificateinitialized = false;
            }
        }
        public string? CertificatePassword { 
            get => _certificatePassword;
            set
            {
                _certificatePassword = value;
                _certificateinitialized = false;
            }
        }
        public X509Certificate2? ServerCertificate {
            get
            {
                if (!_certificateinitialized)
                {
                    if (_certificatePath != null)
                    {
                        _serverCertificate = new X509Certificate2(
                            _certificatePath,
                            _certificatePassword);
                        _certificateinitialized = true;
                    }
                }
                return _serverCertificate;
            }
        }
        public ProxyServer? ProxyServer { get; set; }
        public string? FilesDir { get; set; }
    }

    public enum ActionEnum
    {
        Allow,
        Deny,
        Bypass,
        File
    }
}
