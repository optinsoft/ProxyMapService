namespace ProxyMapService.Proxy.Http
{
    public interface IBodyTracker : IDisposable
    {
        bool Completed { get; }
        bool Failed { get; }
        long BodyLength { get; }
        bool TryAppend(ReadOnlySpan<byte> data);
    }
}
