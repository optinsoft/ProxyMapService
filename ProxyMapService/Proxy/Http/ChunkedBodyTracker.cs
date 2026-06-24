using System.Globalization;
using System.Text;

namespace ProxyMapService.Proxy.Http
{
    public class ChunkedBodyTracker(ILogger logger) : IBodyTracker
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
        private const long MaxChunkSize = 2L * 1024 * 1024 * 1024;

        private readonly byte[] _lineBuffer = new byte[Math.Max(
            MaxChunkHeaderLength,
            MaxTrailerLength)];

        private int _lineLength;

        private int _crlfBytesRead;

        private State _state = State.ReadChunkSize;
        private long _chunkBytesRemaining;

        public bool Completed => _state == State.Completed;

        public bool Failed => _state == State.Failed;

        public bool TryAppend(ReadOnlySpan<byte> data)
        {
            if (Completed)
                return true;

            if (Failed)
                return false;

            try
            {
                int pos = 0;

                while (pos < data.Length && !Completed)
                {
                    switch (_state)
                    {
                        case State.ReadChunkSize:
                            pos = ReadChunkSize(data, pos);
                            break;

                        case State.ReadChunkData:
                            pos = ReadChunkData(data, pos);
                            break;

                        case State.ReadChunkDataCRLF:
                            pos = ReadChunkDataCRLF(data, pos);
                            break;

                        case State.ReadTrailers:
                            pos = ReadTrailers(data, pos);
                            break;

                        default:
                            return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _state = State.Failed;

                logger.LogWarning(
                    ex,
                    "ChunkedBodyTracker parse error: {Message}",
                    ex.Message);

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

                    if (_chunkBytesRemaining > MaxChunkSize)
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
                pos += (int)_chunkBytesRemaining;
                _chunkBytesRemaining = 0;
                _state = State.ReadChunkDataCRLF;
            }
            else
            {
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
                        return pos;
                    }

                    ResetLineBuffer();
                }
            }

            return pos;
        }
    }
}
