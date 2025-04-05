namespace ProxyMapService.Proxy.Handlers
{
    public enum HandleStep
    {
        Initialize,
        Initialized,
        AuthenticationNotRequired,
        Authenticated,
        Terminate,
    }
}
