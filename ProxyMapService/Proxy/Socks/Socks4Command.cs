namespace ProxyMapService.Proxy.Socks
{
    public enum Socks4Command
    {
        Connect = 1,
        RequestGranted = 90,
        RequestRejectedOrFailed = 91,
        RequestRejectedNoIdentd = 92,
        RequestRejectedIdNotConfirmed = 93
    }
}
