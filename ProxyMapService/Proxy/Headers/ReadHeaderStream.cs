using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Headers
{
    public enum ProtocolTypeEnum { Http, Socks4, Socks5, Unknown };

    public class ReadHeaderStream : IDisposable
    {
        private static readonly byte[] Delimiter = [0x0d, 0x0a, 0x0d, 0x0a];
        private const int BufferSize = 8192;

        private readonly MemoryStream _memoryStream = new();
        private readonly byte[] _readBuffer = new byte[BufferSize];
        private int _bufferPos = 0;
        private int _totalRead = 0;
        private ProtocolTypeEnum? _protocolType = null;
        private int _delimiterCounter = 0;

        public ProtocolTypeEnum? ProtocolType { 
            get { return _protocolType; } 
            set { _protocolType = value; }
        }

        public byte SocksVersion => _protocolType switch { 
            ProtocolTypeEnum.Socks4 => 0x04,
            ProtocolTypeEnum.Socks5 => 0x05,
            _ => 0
        };

        public int DelimiterCounter { 
            get { return _delimiterCounter; } 
        }

        public async Task<byte[]?> ReadHeaderBytes(CountingStream? client, CancellationToken token)
        {
            if (client == null) return null;
            client.PauseReadCount();
            try
            {
                if (await ReadByte(client, token) == null)
                {
                    return null;
                }
                return _protocolType switch
                {
                    ProtocolTypeEnum.Http => await ReadHttpHeaderBytes(client, token),
                    ProtocolTypeEnum.Socks4 => await ReadSocks4HeaderBytes(client, token),
                    ProtocolTypeEnum.Socks5 => await ReadSocks5HeaderBytes(client, token),
                    _ => null,
                };
            }
            finally
            {
                if (_bufferPos > 0)
                {
                    client.OnBytesRead(_bufferPos, _readBuffer, 0);
                    _bufferPos = 0;
                }
                client.ResumeReadCount();
            }
        }

        private async Task<byte?> ReadByte(CountingStream client, CancellationToken token)
        {
            if (_bufferPos >= BufferSize)
            {
                client.OnBytesRead(_bufferPos, _readBuffer, 0);
                _bufferPos = 0;
            }

            int bytesRead = await client.ReadAsync(_readBuffer.AsMemory(_bufferPos, 1), token);
            if (bytesRead <= 0) return null;

            _memoryStream.Write(_readBuffer, _bufferPos, bytesRead);

            if (_totalRead == 0 && _protocolType == null)
            {
                _protocolType = _readBuffer[_bufferPos] switch
                {
                    0x04 => ProtocolTypeEnum.Socks4,
                    0x05 => ProtocolTypeEnum.Socks5,
                    _ => HttpParser.IsHttpTokenChar(_readBuffer[_bufferPos])
                        ? ProtocolTypeEnum.Http 
                        : ProtocolTypeEnum.Unknown
                };
            }

            byte byteValue = _readBuffer[_bufferPos];

            if (_protocolType == ProtocolTypeEnum.Http)
            {
                _delimiterCounter = byteValue == Delimiter[_delimiterCounter] ? _delimiterCounter + 1 : 0;
            }

            _bufferPos += bytesRead;
            _totalRead += bytesRead;

            return byteValue;
        }

        private async Task<byte[]?> ReadHttpHeaderBytes(CountingStream client, CancellationToken token)
        {
            if (_protocolType != ProtocolTypeEnum.Http)
            { 
                return null;
            }
            while (_delimiterCounter < Delimiter.Length)
            {
                if (await ReadByte(client, token) == null)
                {
                    break;
                }
            }
            return _delimiterCounter == Delimiter.Length ? _memoryStream.ToArray() : null;
        }

        private async Task<byte[]?> ReadSocks4HeaderBytes(CountingStream client, CancellationToken token)
        {
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

        private async Task<byte[]?> ReadSocks5HeaderBytes(CountingStream client, CancellationToken token)
        {
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