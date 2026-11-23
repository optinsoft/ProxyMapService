namespace ProxyMapService.Proxy.Configurations
{
    /// <summary>
    /// Proxying direction of a listening port:
    /// Forward — the classic forward proxy (the client names the target host itself),
    /// Reverse — a reverse proxy (the target host is fixed in the ReverseProxy settings).
    /// </summary>
    public enum ModeEnum
    {
        Forward = 0,
        Reverse = 1,
    }
}
