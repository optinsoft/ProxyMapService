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
        public bool DecryptSSL { get; set; }
        public SslMode SslMode { get; private set; } = SslMode.Auto;
        public SslMode UpstreamSslMode { get; private set; } = SslMode.Auto;
    }
}
