namespace ProxyMapService.Proxy.Configurations
{
    /// <summary>
    /// TLS detection settings, read once at proxy startup from the "TlsDetection"
    /// configuration section.
    /// </summary>
    public static class TlsDetectionConfig
    {
        /// <summary>
        /// How long to wait for the full TLS ClientHello record after the first byte
        /// (0x16) has been peeked from the incoming stream. If the record does not
        /// arrive within this time, the connection is treated as non-TLS.
        /// </summary>
        public static TimeSpan ClientHelloPeekTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
    }
}
