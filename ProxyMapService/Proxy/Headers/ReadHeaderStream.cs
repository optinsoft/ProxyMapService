using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Headers
{
    public class ReadHeaderStream(SessionContext context, BytesReadCounter? readCounter) : IDisposable
    {
        private static readonly byte[] Delimiter = [0x0d, 0x0a, 0x0d, 0x0a];
        private const int BufferSize = 8192;

        private readonly SessionContext _context = context;
        private readonly BytesReadCounter? _readCounter = readCounter;

        private readonly MemoryStream _memoryStream = new();
        private readonly byte[] _readBuffer = new byte[BufferSize];
        private int _bufferPos = 0;
        private int _totalRead = 0;
        private byte _socksVersion = 0x0;
        private int _delimiterCounter = 0;
        
        public byte SocksVersion { get { return _socksVersion; } }
        public int DelimiterCounter { get { return _delimiterCounter; } }

        public async Task<byte[]?> ReadHeaderBytes(Stream client, CancellationToken token)
        {
            try
            {
                if (await ReadByte(client, token) == null)
                {
                    return null;
                }
                switch (_socksVersion)
                {
                    case 0x00:
                        return await ReadHttpHeaderBytes(client, token);
                    case 0x04:
                        return await ReadSocks4HeaderBytes(client, token);
                    case 0x05:
                        return await ReadSocks5HeaderBytes(client, token);
                    default:
                        return null;
                }
            }
            finally
            {
                if (_bufferPos > 0)
                {
                    _readCounter?.OnBytesRead(_context, _bufferPos, _readBuffer, 0);
                    _bufferPos = 0;
                }
            }
        }

        private async Task<byte?> ReadByte(Stream client, CancellationToken token)
        {
            if (_bufferPos >= BufferSize)
            {
                _readCounter?.OnBytesRead(_context, _bufferPos, _readBuffer, 0);
                _bufferPos = 0;
            }

            int bytesRead = await client.ReadAsync(_readBuffer.AsMemory(_bufferPos, 1), token);
            if (bytesRead <= 0) return null;

            _memoryStream.Write(_readBuffer, _bufferPos, bytesRead);

            if (_totalRead == 0)
            {
                switch (_readBuffer[_bufferPos])
                {
                    case 0x04:
                        _socksVersion = _readBuffer[_bufferPos];
                        break;
                    case 0x05:
                        _socksVersion = _readBuffer[_bufferPos];
                        break;
                }
            }

            byte byteValue = _readBuffer[_bufferPos];

            if (_socksVersion == 0x0)
            {
                _delimiterCounter = byteValue == Delimiter[_delimiterCounter] ? _delimiterCounter + 1 : 0;
            }

            _bufferPos += bytesRead;
            _totalRead += bytesRead;

            return byteValue;
        }

        private async Task<byte[]?> ReadHttpHeaderBytes(Stream client, CancellationToken token)
        {
            while (_socksVersion == 0x0 && _delimiterCounter < Delimiter.Length)
            {
                if (await ReadByte(client, token) == null)
                {
                    break;
                }
            }
            return _socksVersion == 0x0 && _delimiterCounter == Delimiter.Length ? _memoryStream.ToArray() : null;
        }

        private async Task<byte[]?> ReadSocks4HeaderBytes(Stream client, CancellationToken token)
        {
            if (_socksVersion != 0x04) return null;
            byte? CD = await ReadByte(client, token);
            if (CD == null)
            {
                return null;
            }
            for (int i = 0; i < 6; i++) {
                if (await ReadByte(client, token) == null)
                {
                    break;
                }

            }
            if (_totalRead < 8) return null;
            if (CD == 1)
            {
                byte? nullByte;
                do
                {
                    nullByte = await ReadByte(client, token);
                    if (nullByte == null)
                    {
                        return null;
                    }
                } 
                while (nullByte != 0x0);
            }
            return _memoryStream.ToArray();
        }

        private async Task<byte[]?> ReadSocks5HeaderBytes(Stream client, CancellationToken token)
        {
            if (_socksVersion != 0x05) return null;

            byte? NMethods = await ReadByte(client, token);

            if (NMethods == null)
            {
                return null;
            }

            for (int i = 0; i < (int)NMethods; ++i)
            {
                if (await ReadByte(client, token) == null)
                {
                    break;
                }
            }

            return _totalRead == (int)NMethods + 2 ? _memoryStream.ToArray() : null;
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
                _memoryStream.Dispose();
            }
        }
    }
}