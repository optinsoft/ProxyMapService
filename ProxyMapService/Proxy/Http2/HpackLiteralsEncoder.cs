using System.Buffers;
using System.Text;
using ProxyMapService.Proxy.Http2.HPack;

namespace ProxyMapService.Proxy.Http2
{
    /// <summary>
    /// Stateless HPACK encoder. Every field is emitted as a non-indexing literal, so no dynamic table
    /// state is ever created on either side because of our output:
    ///   - a name from the static table: "Literal without Indexing, Indexed Name"  (0000 + Index(4+), RFC 7541 6.2.2);
    ///   - any other name:               "Literal without Indexing, New Name"      (0000 0000 + Name(7+), RFC 7541 6.2.2).
    /// Values are plain strings without Huffman coding.
    /// Used for re-encoding header blocks whose original (client's) HPACK encoding must not be forwarded as is.
    /// </summary>
    public static class HpackLiteralsEncoder
    {
        private static readonly Dictionary<string, int> NameIndex = new();

        static HpackLiteralsEncoder()
        {
            // H2StaticTable.Get() takes a 0-based index, whereas HPACK indices are 1-based
            for (int i = 0; i < H2StaticTable.Count; i++)
            {
                var f = H2StaticTable.Get(i);
                string n = Encoding.Latin1.GetString(f.Name);
                NameIndex.TryAdd(n, i + 1);
            }
        }

        public static byte[] Encode(IReadOnlyList<H2Header> headers)
        {
            var w = new ArrayBufferWriter<byte>(256);
            foreach (var (name, value) in headers)
            {
                var nameBytes = Encoding.Latin1.GetBytes(name);
                var valueBytes = Encoding.Latin1.GetBytes(value);

                if (NameIndex.TryGetValue(name, out int index))
                {
                    EmitInteger(w, 0, index, 4);          // 0000 + Index(4+)
                }
                else
                {
                    EmitInteger(w, 0, 0, 4);              // 0000 0000 — new name
                    EmitInteger(w, 0, nameBytes.Length, 7); // H=0 + Name Length(7+)
                    nameBytes.CopyTo(w.GetSpan(nameBytes.Length));
                    w.Advance(nameBytes.Length);
                }

                EmitInteger(w, 0, valueBytes.Length, 7);  // H=0 + Value Length(7+)
                valueBytes.CopyTo(w.GetSpan(valueBytes.Length));
                w.Advance(valueBytes.Length);
            }
            return w.WrittenSpan.ToArray();
        }

        /// <summary>HPACK integer (RFC 7541, section 5.1): <paramref name="prefixBits"/>-bit prefix carrying <paramref name="highBits"/>, then 7-bit continuation groups.</summary>
        private static void EmitInteger(ArrayBufferWriter<byte> w, int highBits, int value, int prefixBits)
        {
            int mask = (1 << prefixBits) - 1;
            if (value <= mask)
            {
                WriteByte(w, (byte)((highBits << (8 - prefixBits)) | value));
            }
            else
            {
                WriteByte(w, (byte)((highBits << (8 - prefixBits)) | mask));
                int rest = value - mask;
                while (rest >= 128)
                {
                    WriteByte(w, (byte)(0x80 | (rest & 0x7F)));
                    rest >>= 7;
                }
                WriteByte(w, (byte)rest);
            }
        }

        private static void WriteByte(ArrayBufferWriter<byte> w, byte b)
        {
            w.GetSpan(1)[0] = b;
            w.Advance(1);
        }
    }
}
