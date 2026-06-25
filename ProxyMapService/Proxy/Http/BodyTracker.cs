using ProxyMapService.Proxy.Counters;
using System.IO;

namespace ProxyMapService.Proxy.Http
{
    public class BodyTracker(ILogger logger, string? contentType, long contentLength, IHttpBodyLogger? bodyLogger, object context, bool shouldAccumulate) : IBodyTracker
    {
        private enum State
        {
            ReadData,
            Completed,
            Failed,
            Disposed
        }

        private State _state = State.ReadData;

        private long _bodyLength;
        private MemoryStream? _bodyStream = shouldAccumulate ? new() : null;

        public bool Completed => _state == State.Completed;

        public bool Failed => _state == State.Failed;

        public long BodyLength => _bodyLength;

        public bool TryAppend(ReadOnlySpan<byte> data)
        {
            if (_state == State.Disposed)
                throw new ObjectDisposedException(nameof(BodyTracker));

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
                if (bodyLogger != null)
                {
                    bodyLogger.OnCompleted(context, contentType, _bodyLength, _bodyStream);
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (_state == State.Disposed)
                return;

            _state = State.Disposed;
            _bodyStream?.Dispose();
        }
    }
}
