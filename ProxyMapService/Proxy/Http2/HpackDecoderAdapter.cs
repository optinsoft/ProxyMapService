using System.Text;
using ProxyMapService.Proxy.Http2.HPack;

namespace ProxyMapService.Proxy.Http2
{
    /// <summary>IHpackDecoder on top of HPackDecoder from dotnet/runtime. One instance per connection.</summary>
    public sealed class HpackDecoderAdapter : IHpackDecoder
    {
        private const int MaxStringLength = 64 * 1024;       // limit for a single line (name or value)
        private const int MaxHeaderListSize = 256 * 1024;    // limit for the entire header list

        private readonly HPackDecoder _decoder = new(HPackDecoder.DefaultHeaderTableSize, MaxStringLength);
        private readonly Collector _collector = new(MaxHeaderListSize);

        public List<H2Header> Decode(ReadOnlySpan<byte> block)
        {
            _collector.Reset();
            try
            {
                // endHeaders: true itself verifies that the block is not truncated (CheckIncompleteHeaderBlock)
                _decoder.Decode(block, endHeaders: true, _collector);
            }
            catch (HPackDecodingException ex)
            {
                throw new InvalidDataException("HPACK decoding failed: " + ex.Message, ex);
            }
            return _collector.Headers;
        }

        private sealed class Collector(int maxSize) : IHttpStreamHeadersHandler
        {
            private int _size;
            public List<H2Header> Headers { get; private set; } = [];

            public void Reset() { Headers = []; _size = 0; }

            public void OnStaticIndexedHeader(int index)
            {
                var f = H2StaticTable.Get(index - 1);          // index is 1-based, Get() expects 0-based
                Add(f.Name, f.Value);
            }

            public void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value)
                => Add(H2StaticTable.Get(index - 1).Name, value);

            public void OnDynamicIndexedHeader(int? staticTableIndex, ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
                => Add(name, value);

            public void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
                => Add(name, value);

            public void OnHeadersComplete(bool endStream) { }

            private void Add(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
            {
                _size += name.Length + value.Length + 32;      // 32 — overhead per RFC 7541
                if (_size > maxSize) throw new InvalidDataException("Header list is too large");
                Headers.Add(new H2Header(Encoding.Latin1.GetString(name), Encoding.Latin1.GetString(value)));
            }
        }
    }
}
