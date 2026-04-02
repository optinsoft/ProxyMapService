using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Configurations
{
    public class SslCertificateConfig
    {
        private string? _path = null;
        private string? _password = null;
        private StoreLocation _storeLocation = StoreLocation.LocalMachine;
        private StoreName _storeName = StoreName.My;
        private string? _subject = null;
        private X509Certificate2? _certificate = null;
        private bool _initialized = false;

        public string? Path
        {
            get => _path;
            set
            {
                _path = value;
                _initialized = false;
            }
        }
        public string? Password
        {
            get => _password;
            set
            {
                _password = value;
                _initialized = false;
            }
        }
        public StoreLocation StoreLocation { 
            get => _storeLocation; 
            set
            {
                _storeLocation = value;
                _initialized = false;
            }
        }
        public StoreName StoreName
        {
            get => _storeName;
            set
            {
                _storeName = value;
                _initialized = false;
            }
        }
        public string? Subject
        {
            get => _subject;
            set
            {
                _subject = value;
                _initialized = false;
            }
        }
        public X509Certificate2? Certificate
        {
            get
            {
                if (!_initialized)
                {
                    if (_path != null)
                    {
                        _certificate = new X509Certificate2(
                            _path,
                            _password);
                    }
                    else if (_subject != null)
                    {
                        using var store = new X509Store(_storeName, _storeLocation);
                        store.Open(OpenFlags.ReadOnly);
                        _certificate = store.Certificates
                            .Find(
                                X509FindType.FindBySubjectDistinguishedName,
                                _subject,
                                false)
                            .OfType<X509Certificate2>()
                            .OrderByDescending(c => c.NotBefore)
                            .First();
                    }
                    else
                    {
                        _certificate = null;
                    }
                    _initialized = true;
                }
                return _certificate;
            }
        }
    }
}
