using ProxyMapService.Proxy.Http2.HPack;
using System.Buffers;
using System.Text;

namespace ProxyMapService.Proxy.Http2
{
    /// <summary>IHpackEncoder on top of HPackEncoder from dotnet/runtime + static indexing.</summary>
    public sealed class HpackEncoderAdapter : IHpackEncoder
    {
        private static readonly Dictionary<string, int> NameIndex = new();
        private static readonly Dictionary<(string, string), int> PairIndex = new();

        static HpackEncoderAdapter()
        {
            // H2StaticTable.Get() takes a 0-based index, whereas HPACK indices are 1-based
            for (int i = 0; i < H2StaticTable.Count; i++)
            {
                var f = H2StaticTable.Get(i);
                string n = Encoding.Latin1.GetString(f.Name);
                string v = Encoding.Latin1.GetString(f.Value);
                NameIndex.TryAdd(n, i + 1);
                PairIndex.TryAdd((n, v), i + 1);
            }
        }

        private static bool IsSensitive(string name)
            => name is "authorization" or "cookie" or "proxy-authorization" or "set-cookie";

        public byte[] Encode(IReadOnlyList<H2Header> headers)
        {
            var w = new ArrayBufferWriter<byte>(256);
            foreach (var (name, value) in headers)
            {
                // the runtime encoder checks the ASCII-ness of the name only via Debug.Assert
                if (!Ascii.IsValid(name))
                    throw new InvalidDataException("Header name must be ASCII: " + name);

                // headroom for length and index prefixes (up to 5 bytes each), so "out of space" is impossible
                var span = w.GetSpan(name.Length + value.Length + 32);
                bool ok;
                int n;

                if (PairIndex.TryGetValue((name, value), out int idx))
                {
                    ok = HPackEncoder.EncodeIndexedHeaderField(idx, span, out n);
                }
                else
                {
                    bool sensitive = IsSensitive(name);
                    if (NameIndex.TryGetValue(name, out idx))
                    {
                        ok = sensitive
                            ? HPackEncoder.EncodeLiteralHeaderFieldNeverIndexing(idx, value, Encoding.Latin1, span, out n)
                            : HPackEncoder.EncodeLiteralHeaderFieldWithoutIndexing(idx, value, Encoding.Latin1, span, out n);
                    }
                    else
                    {
                        ok = sensitive
                            ? HPackEncoder.EncodeLiteralHeaderFieldNeverIndexingNewName(name, value, Encoding.Latin1, span, out n)
                            : HPackEncoder.EncodeLiteralHeaderFieldWithoutIndexingNewName(name, value, Encoding.Latin1, span, out n);
                    }
                }

                if (!ok) throw new InvalidOperationException("HPACK encode buffer is too small");
                w.Advance(n);
            }
            return w.WrittenSpan.ToArray();
        }
    }
}
