using ProxyMapService.Proxy.Counters;
using System.Globalization;
using System.Text;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Http
{
    public class ChunkedBodyTracker(ILogger logger, bool response, HttpRequestHeader? requestHeader, string? contentType, string? contentEncoding,
        bool hasBody, IHttpBodyLogger? bodyLogger, object context, bool shouldAccumulate, MemoryStream? accumulateStream) : IBodyTracker
    {
        private enum State
        {
            ReadChunkSize,
            ReadChunkData,
            ReadChunkDataCRLF,
            ReadTrailers,
            Completed,
            Failed
        }

        private const int MaxChunkHeaderLength = 256;
        private const int MaxTrailerLength = 8192;
        private const int MaxTrailerCount = 64;
        private const long MaxChunkSize = 2L * 1024 * 1024 * 1024;

#if DEBUG
        private readonly string _logPrefix = response ? "[response] ChunkedBodyTracker: " : "[request] ChunkedBodyTracker: ";
#endif
        private readonly string _logErrorPrefix = response ? "[response] ChunkedBodyTracker error: " : "[request] ChunkedBodyTracker error: ";

        private readonly byte[] _lineBuffer = new byte[Math.Max(
            MaxChunkHeaderLength,
            MaxTrailerLength)];

        private int _lineLength;

        private int _crlfBytesRead;

        private State _state = hasBody ? State.ReadChunkSize : State.Completed;
        private long _chunkBytesRemaining;

        private long _bodyLength;
        private MemoryStream? _bodyStream = shouldAccumulate && accumulateStream == null ? new() : null;

        private readonly List<(int Offset, int Length)> _payload = new(2);
        private readonly List<string> _trailers = new();

        public bool Completed => _state == State.Completed;

        public bool Failed => _state == State.Failed;

        public long BodyLength => _bodyLength;

        public IReadOnlyList<(int Offset, int Length)> Payload => _payload;

        public IReadOnlyList<string>? Trailers => _trailers;

        public bool TryAppend(ReadOnlySpan<byte> data, out int consumed)
        {
            _payload.Clear();

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

            int pos = 0;

            try
            {
                while (pos < data.Length && !Completed)
                {
                    pos = _state switch
                    {
                        State.ReadChunkSize => ReadChunkSize(data, pos),
                        State.ReadChunkData => ReadChunkData(data, pos),
                        State.ReadChunkDataCRLF => ReadChunkDataCRLF(data, pos),
                        State.ReadTrailers => ReadTrailers(data, pos),
                        _ => throw new InvalidOperationException()
                    };
                }

                consumed = pos;
                return true;
            }
            catch (Exception ex)
            {
                _state = State.Failed;

                logger.LogWarning(
                    ex,
                    "{prefix}{Message}",
                    _logErrorPrefix,
                    ex.Message);

                consumed = pos;
                return false;
            }
        }

        private void AppendLineByte(byte b, int maxLength, string description)
        {
            if (_lineLength >= maxLength)
            {
                throw new InvalidDataException(
                    $"{description} exceeds maximum allowed size ({maxLength} bytes).");
            }

            _lineBuffer[_lineLength++] = b;
        }

        private void ResetLineBuffer()
        {
            _lineLength = 0;
        }

        private int ReadChunkSize(ReadOnlySpan<byte> data, int pos)
        {
            while (pos < data.Length)
            {
                byte b = data[pos++];

                AppendLineByte(
                    b,
                    MaxChunkHeaderLength,
                    "Chunk header");

                int count = _lineLength;

                if (count >= 2 &&
                    _lineBuffer[count - 2] == '\r' &&
                    _lineBuffer[count - 1] == '\n')
                {
                    string line = Encoding.ASCII.GetString(
                        _lineBuffer,
                        0,
                        count - 2);

                    ResetLineBuffer();

                    int semicolon = line.IndexOf(';');
                    if (semicolon >= 0)
                    {
                        line = line[..semicolon];
                    }

                    line = line.Trim();

                    if (!long.TryParse(
                        line,
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out _chunkBytesRemaining))
                    {
                        throw new InvalidDataException(
                            $"Invalid chunk size '{line}'.");
                    }

                    if (_chunkBytesRemaining < 0 || _chunkBytesRemaining > MaxChunkSize)
                    {
                        throw new InvalidDataException(
                            $"Chunk size {_chunkBytesRemaining} exceeds limit.");
                    }

                    _state = _chunkBytesRemaining == 0
                        ? State.ReadTrailers
                        : State.ReadChunkData;

                    break;
                }
            }

            return pos;
        }

        private int ReadChunkData(ReadOnlySpan<byte> data, int pos)
        {
            int available = data.Length - pos;

            if (available >= _chunkBytesRemaining)
            {
                int n = (int)_chunkBytesRemaining;
                _payload.Add((pos, n));
                _bodyLength += n;
                if (shouldAccumulate)
                {
                    accumulateStream?.Write(data.Slice(pos, n));
                    _bodyStream?.Write(data.Slice(pos, n));
                }
                pos += n;
                _chunkBytesRemaining = 0;
                _state = State.ReadChunkDataCRLF;
            }
            else
            {
                _payload.Add((pos, available));
                _bodyLength += available;
                if (shouldAccumulate)
                {
                    accumulateStream?.Write(data.Slice(pos, available));
                    _bodyStream?.Write(data.Slice(pos, available));
                }
                _chunkBytesRemaining -= available;
                pos = data.Length;
            }

            return pos;
        }

        private int ReadChunkDataCRLF(ReadOnlySpan<byte> data, int pos)
        {
            while (pos < data.Length)
            {
                byte b = data[pos++];

                switch (_crlfBytesRead)
                {
                    case 0:
                        if (b != '\r')
                            throw new InvalidDataException(
                                "Chunk data terminator CRLF expected.");

                        _crlfBytesRead = 1;
                        break;

                    case 1:
                        if (b != '\n')
                            throw new InvalidDataException(
                                "Chunk data terminator CRLF expected.");

                        _crlfBytesRead = 0;
                        _state = State.ReadChunkSize;

                        return pos;
                }
            }

            return pos;
        }

        private int ReadTrailers(ReadOnlySpan<byte> data, int pos)
        {
            while (pos < data.Length)
            {
                byte b = data[pos++];

                AppendLineByte(
                    b,
                    MaxTrailerLength,
                    "Chunk trailer");

                if (_lineLength >= 2 &&
                    _lineBuffer[_lineLength - 2] == '\r' &&
                    _lineBuffer[_lineLength - 1] == '\n')
                {
                    if (_lineLength == 2)
                    {
                        _state = State.Completed;
                        bodyLogger?.OnCompleted(context, requestHeader, contentType, contentEncoding, _bodyLength, accumulateStream ?? _bodyStream);
                        return pos;
                    }

                    if (_trailers.Count >= MaxTrailerCount)
                    {
                        throw new InvalidDataException(
                            $"Too many chunk trailers (max {MaxTrailerCount}).");
                    }

                    _trailers.Add(Encoding.Latin1.GetString(_lineBuffer, 0, _lineLength - 2));

                    ResetLineBuffer();
                }
            }

            return pos;
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
