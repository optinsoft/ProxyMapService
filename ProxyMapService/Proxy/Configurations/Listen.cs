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

        public bool RejectHttpProxy { get; set; } = false;
        public int StickyProxyLifetime { get; set; } = 0;
        public ActionEnum Action { get; set; } = ActionEnum.Allow;
        public bool DecryptSSL { get; set; } = false;
        public SslMode SslMode { get; set; } = SslMode.Auto;
        public SslMode UpstreamSslMode { get; set; } = SslMode.Auto;
    }
}
