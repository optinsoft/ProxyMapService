using ProxyMapService.Proxy.Counters;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Http
{
    public class BodyTracker(ILogger logger, bool response, HttpRequestHeader? requestHeader, string? contentType, string? contentEncoding, long contentLength, 
        bool hasBody, IHttpBodyLogger? bodyLogger, object context, bool shouldAccumulate, MemoryStream? accumulateStream) : IBodyTracker
    {
        private enum State
        {
            ReadData,
            Completed,
            Failed
        }

#if DEBUG
        private readonly string _logPrefix = response ? "[response] BodyTracker: " : "[request] BodyTracker: ";
#endif
        private readonly string _logErrorPrefix = response ? "[response] BodyTracker error: " : "[request] BodyTracker error: ";

        private State _state = hasBody ? State.ReadData : State.Completed;

        private long _bodyLength;
        private MemoryStream? _bodyStream = shouldAccumulate && accumulateStream == null ? new() : null;

        private readonly List<(int Offset, int Length)> _payload = new(1);

        public bool Completed => _state == State.Completed;

        public bool Failed => _state == State.Failed;

        public long BodyLength => _bodyLength;

        public IReadOnlyList<(int Offset, int Length)> Payload => _payload;

        public IReadOnlyList<string>? Trailers => null;

        public bool TryAppend(ReadOnlySpan<byte> data, out int consumed)
        {
            _payload.Clear();

#if DEBUG
            logger.LogDebug(
                "{prefix}append data length: {length}.",
                _logPrefix,
                data.Length
                );
#endif

            if (data.Length == 0)
            {
                logger.LogWarning(
                    "{prefix}empty data.",
                    _logErrorPrefix
                    );
                consumed = 0;
                return false;
            }

            if (Completed)
            {
                logger.LogWarning(
                    "{prefix}body already completed.",
                    _logErrorPrefix
                    );
                consumed = 0;
                return false;
            }

            if (Failed)
            {
                consumed = 0;
                return false;
            }

            consumed = (int)Math.Min(contentLength - _bodyLength, data.Length);

            if (consumed > 0)
            {
                var part = data[..consumed];

                _payload.Add((0, consumed));

                _bodyLength += consumed;
                if (shouldAccumulate)
                {
                    accumulateStream?.Write(part);
                    _bodyStream?.Write(part);
                }
            }

            if (_bodyLength >= contentLength)
            {
                _state = State.Completed;
                bodyLogger?.OnCompleted(context, requestHeader, contentType, contentEncoding, _bodyLength, accumulateStream ?? _bodyStream);
            }

            return true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_bodyStream != null)
                {
                    using (_bodyStream)
                    {
                    }
                    _bodyStream = null;
                }
            }
        }
    }
}
