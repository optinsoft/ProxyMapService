namespace ProxyMapService.Proxy.Http
{
    public interface IBodyTracker : IDisposable
    {
        bool Completed { get; }
        bool Failed { get; }
        long BodyLength { get; }
        
        /// <summary>
        /// Payload ranges (without chunk framing) relative to the span
        /// passed to the last TryAppend call. Reset at the beginning of each TryAppend.
        /// </summary>
        IReadOnlyList<(int Offset, int Length)> Payload { get; }

        /// <summary>Trailer lines without CRLF ("name: value"). Populated by the time Completed is reached; always null for BodyTracker.</summary>
        IReadOnlyList<string>? Trailers { get; }

        bool TryAppend(ReadOnlySpan<byte> data, out int consumed);
    }
}
