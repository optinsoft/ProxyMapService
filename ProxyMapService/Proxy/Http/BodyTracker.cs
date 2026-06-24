using Fare;

namespace ProxyMapService.Proxy.Http
{
    public class BodyTracker(ILogger logger, long contentLength) : IBodyTracker
    {
        private enum State
        {
            ReadData,
            Completed,
            Failed
        }

        private long _bodyLength;

        private State _state = State.ReadData;

        public bool Completed => _state == State.Completed;

        public bool Failed => _state == State.Failed;

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

            if (_bodyLength >= contentLength)
            {
                _state = State.Completed;
            }

            return true;
        }
    }
}
