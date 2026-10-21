namespace ProxyMapService.Proxy.Http2.HPack
{
    internal static class SR
    {
        public static string net_http_invalid_header_name => "Received an invalid header name: '{0}'.";
        public static string net_http_hpack_unexpected_end => "End of header block reached unexpectedly while decoding HPACK data.";
        public static string net_http_hpack_late_dynamic_table_size_update => "Dynamic table size update received after the first header of the block.";
        public static string net_http_hpack_large_table_size_update => "Dynamic table size update {0} exceeds the allowed limit.";
        public static string net_http_hpack_invalid_index => "Invalid HPACK table index {0}.";
        public static string net_http_headers_exceeded_length => "The HTTP headers length exceeded the set limit of {0} bytes.";
        public static string net_http_hpack_huffman_decode_failed => "Huffman decoding failed.";
        public static string net_http_hpack_incomplete_header_block => "Incomplete header block.";
        public static string net_http_hpack_bad_integer => "Invalid HPACK integer.";

        public static string Format(string format, params object?[] args)
        {
            try { return string.Format(format, args); }
            catch (FormatException) { return format; }
        }
    }
}
