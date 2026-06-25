using ProxyMapService.Proxy.Counters;

namespace ProxyMapService.Proxy.Http
{
    public class BodyTracker(ILogger logger, string? contentType, long contentLength, IHttpBodyLogger? bodyLogger, object context, bool shouldAccumulate) : IBodyTracker
    {
        private enum State
        {
            ReadData,
            Completed,
            Failed
        }

        private State _state = State.ReadData;

        private long _bodyLength;
        private MemoryStream? _bodyStream = shouldAccumulate ? new() : null;

        public bool Completed => _state == State.Completed;

        public bool Failed => _state == State.Failed;

        public long BodyLength => _bodyLength;

        public bool TryAppend(ReadOnlySpan<byte> data)
        {
            if (Completed)
                return true;

            if (Failed)
                return false;

            if (_bodyLength + data.Length > contentLength)
            {
                _state = State.Failed;

                logger.LogWarning(
                    "BodyTracker error: body data length exceeded the expected content length ({contentLength} bytes).",
                    contentLength
                    );

                return false;
            }

            _bodyLength += data.Length;
            _bodyStream?.Write(data);

            if (_bodyLength >= contentLength)
            {
                _state = State.Completed;
                bodyLogger?.OnCompleted(context, contentType, _bodyLength, _bodyStream);
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
