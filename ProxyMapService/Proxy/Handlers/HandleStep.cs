namespace ProxyMapService.Proxy.Handlers
{
    public enum HandleStep
    {
        Initialize,
        Initialized,
        AuthenticationNotRequired,
        Authenticated,
        Proxy,
        Bypass,
        Tunnel,
        Terminate,
    }
}
