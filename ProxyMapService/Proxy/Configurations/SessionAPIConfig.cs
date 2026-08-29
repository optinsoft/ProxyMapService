namespace ProxyMapService.Proxy.Configurations
{
    public class SessionAPIConfig
    {
        public bool Enabled { get; set; }
        public bool WebSocketsEnabled { get; set; }
        public string Domain { get; set; } = string.Empty;
    }
}
