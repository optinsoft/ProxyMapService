namespace ProxyMapService.Proxy.Http2
{
    /// <summary>Wrapper over the HPACK decoder (copied from dotnet/runtime).</summary>
    public interface IHpackDecoder
    {
        List<H2Header> Decode(ReadOnlySpan<byte> block);
    }
}
