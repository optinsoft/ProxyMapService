namespace ProxyMapService.Proxy.Configurations
{
    public class Listen(int port)
    {
        private PortRange _portRange = new(port, port);

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
    }
}
