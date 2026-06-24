namespace ProxyMapService.Proxy.Http
{
    public interface IBodyTracker
    {
        bool Completed { get; }
        bool Failed { get; }
        bool TryAppend(ReadOnlySpan<byte> data);
    }
}
