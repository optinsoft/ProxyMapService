namespace ProxyMapService.Proxy.Sessions
{
    public class TunnelState
    {
        private static long _currentTunnelId = 0;

        private long _tunnelId = ++_currentTunnelId;

        public required bool Response;
        public long TunnelId { 
            get => _tunnelId;
        }
        public bool ResetReadHeaders;
    }
}
