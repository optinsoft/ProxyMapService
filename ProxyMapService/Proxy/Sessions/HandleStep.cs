namespace ProxyMapService.Proxy.Sessions
{
    public enum HandleStep
    {
        Initialize,
        HttpInitialized,
        HttpAuthenticationNotRequired,
        HttpAuthenticated,
        HttpProxy,
        HttpBypass,
        Socks5Initialized,
        Socks5AuthenticationNotRequired,
        Socks5UsernamePasswordAuthentication,
        Socks5Authenticated,
        Socks5ConnectRequested,
        Socks5Proxy,
        Socks5Bypass,
        Tunnel,
        Terminate,
    }
}
