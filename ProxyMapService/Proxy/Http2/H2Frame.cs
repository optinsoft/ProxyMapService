namespace ProxyMapService.Proxy.Http2
{
    public readonly record struct H2Frame(byte Type, byte Flags, int StreamId, byte[] Payload);
}
