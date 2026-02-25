using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Configurations
{
    public class Listen
    {
        private PortRange _portRange = new();

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
    }
}
