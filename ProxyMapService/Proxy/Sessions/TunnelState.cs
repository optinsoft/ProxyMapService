namespace ProxyMapService.Proxy.Sessions
{
    public class TunnelState
    {
        private static long _currentTunnelId = 0;
        private long _tunnelId = ++_currentTunnelId;
        public bool IsSecure;
        public bool IsHttp2;
        public long TunnelId { 
            get => _tunnelId;
        }
        public bool ResetReadHeaders;
    }
}
