namespace ProxyMapService.Proxy.Http2
{
    /// <summary>Wrapper over the HPACK encoder (copied from dotnet/runtime).</summary>
    public interface IHpackEncoder
    {
        byte[] Encode(IReadOnlyList<H2Header> headers);
    }
}
