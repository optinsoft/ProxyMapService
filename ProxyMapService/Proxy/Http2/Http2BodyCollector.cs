using ProxyMapService.Proxy.Counters;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Http2
{
    /// <summary>Accumulates the body of a single message and, once complete, hands it to IHttpBodyLogger (like BodyTracker in h1).</summary>
    internal sealed class Http2BodyCollector(IHttpBodyLogger? bodyLogger, object context, HttpRequestHeader? requestHeader,
        string? contentType, string? contentEncoding) : IDisposable
    {
        private MemoryStream? _stream = bodyLogger != null ? new MemoryStream() : null;
        private long _length;
        private bool _completed;

        public void Append(ReadOnlySpan<byte> data)
        {
            if (_completed) return;
            _length += data.Length;
            _stream?.Write(data);
        }

        public void Complete()
        {
            if (_completed) return;
            _completed = true;
            // as in h1: a message without a body is not passed to the body logger
            if (_length > 0)
            {
                bodyLogger?.OnCompleted(context, requestHeader, contentType, contentEncoding, _length, _stream);
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

}
