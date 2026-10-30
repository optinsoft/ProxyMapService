using System.Buffers.Binary;

namespace ProxyMapService.Proxy.Http2
{
    /// <summary>Accumulates bytes and yields whole frames.</summary>
    public sealed class H2FrameReader(int MaxFrameSize = 16384) // our SETTINGS_MAX_FRAME_SIZE (default)
    {
        private byte[] _buf = new byte[32 * 1024];
        private int _start, _end;

        public void Append(ReadOnlySpan<byte> data)
        {
            if (_buf.Length - _end < data.Length)
            {
                int len = _end - _start;
                if (_buf.Length - len < data.Length)
                    Array.Resize(ref _buf, Math.Max(_buf.Length * 2, len + data.Length));
                Buffer.BlockCopy(_buf, _start, _buf, 0, len);
                _start = 0;
                _end = len;
            }
            data.CopyTo(_buf.AsSpan(_end));
            _end += data.Length;
        }

        public bool TryRead(out H2Frame frame)
        {
            frame = default;
            int avail = _end - _start;
            if (avail < 9) return false;
            var s = _buf.AsSpan(_start, avail);
            int len = (s[0] << 16) | (s[1] << 8) | s[2];
            if (len > MaxFrameSize) throw new InvalidDataException("HTTP/2 frame is too large");
            if (avail < 9 + len) return false;
            frame = new H2Frame(s[3], s[4], BinaryPrimitives.ReadInt32BigEndian(s.Slice(5, 4)) & 0x7FFFFFFF,
                s.Slice(9, len).ToArray());
            _start += 9 + len;
            if (_start == _end) _start = _end = 0;
            return true;
        }
    }
}
