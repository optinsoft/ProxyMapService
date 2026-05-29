namespace ProxyMapService.Proxy.Socks
{
    public static class Socks5StatusExtensions
    {
        public static string ToLogMessage(this Socks5Status status) => status switch
        {
            Socks5Status.GeneralFailure => "General SOCKS server failure.",
            Socks5Status.ConnectionNotAllowed => "Connection not allowed by ruleset.",
            Socks5Status.NetworkUnreachable => "Network unreachable.",
            Socks5Status.HostUnreachable => "Host unreachable.",
            Socks5Status.ConnectionRefused => "Connection refused by destination host.",
            Socks5Status.TTLExpired => "TTL expired.",
            Socks5Status.CommandNotSupported => "SOCKS command not supported.",
            Socks5Status.AddressTypeNotSupported => "Address type not supported.",
            _ => $"Unknown SOCKS5 error code: {status} (0x{(byte)status:X2})."
        };
    }
}
