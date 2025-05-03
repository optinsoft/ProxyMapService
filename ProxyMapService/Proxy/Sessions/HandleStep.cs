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
        Socks4Initialized,
        Socks4Proxy,
        Socks4Bypass,
        Socks5Initialized,
        Socks5AuthenticationNotRequired,
        Socks5UsernamePasswordAuthentication,
        Socks5Authenticated,
        Socks5ConnectRequested,
        Socks5Proxy,
        Socks5Bypass,
        Proxy,
        Tunnel,
        Terminate,
    }
}
