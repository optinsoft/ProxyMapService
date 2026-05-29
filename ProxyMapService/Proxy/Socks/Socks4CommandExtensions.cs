namespace ProxyMapService.Proxy.Socks
{
    public static class Socks4CommandExtensions
    {
        public static string ToLogMessage(this Socks4Command command) => command switch
        {
            Socks4Command.RequestRejectedOrFailed => "Request rejected or failed.",
            Socks4Command.RequestRejectedNoIdentd => "Request rejected: SOCKS server cannot connect to identd on the client.",
            Socks4Command.RequestRejectedIdNotConfirmed => "Request rejected: Client's identd reported a different user-id.",
            Socks4Command.Connect => "Unexpected status: Connect command cannot be used as a response code.",
            Socks4Command.RequestGranted => "Request granted successfully.",
            _ => $"Unknown SOCKS4 status code: {command} ({(byte)command})."
        };
    }
}
