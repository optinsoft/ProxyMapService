namespace ProxyMapService.Proxy.Http2
{
    internal sealed class H2ConnectionException(uint code, string message) : Exception(message)
    {
        public uint Code { get; } = code;
    }
}
