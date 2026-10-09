using ProxyMapService.Proxy.Counters;

namespace ProxyMapService.Proxy.Http
{
    public class BodyTracker(ILogger logger, bool response, string? contentType, string? contentEncoding, long contentLength, 
        IHttpBodyLogger? bodyLogger, object context, bool shouldAccumulate, MemoryStream? accumulateStream) : IBodyTracker
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

        private State _state = contentLength > 0 ? State.ReadData : State.Completed;

        private long _bodyLength;
        private MemoryStream? _bodyStream = shouldAccumulate && accumulateStream == null ? new() : null;

        public bool Completed => _state == State.Completed;

        public bool Failed => _state == State.Failed;

        public long BodyLength => _bodyLength;

        public bool TryAppend(ReadOnlySpan<byte> data)
        {
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
                return false;
            }

            if (Completed)
            {
                logger.LogWarning(
                    "{prefix}body already completed.",
                    _logErrorPrefix
                    );
                return false;
            }

            if (Failed)
                return false;

            if (_bodyLength + data.Length > contentLength)
            {
                _state = State.Failed;

                logger.LogWarning(
                    "{prefix}body data length exceeded the expected content length ({contentLength} bytes).",
                    _logErrorPrefix,
                    contentLength
                    );

                return false;
            }

            _bodyLength += data.Length;
            if (shouldAccumulate)
            {
                accumulateStream?.Write(data);
                _bodyStream?.Write(data);
            }

            if (_bodyLength >= contentLength)
            {
                _state = State.Completed;
                bodyLogger?.OnCompleted(context, contentType, contentEncoding, _bodyLength, accumulateStream ?? _bodyStream);
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
