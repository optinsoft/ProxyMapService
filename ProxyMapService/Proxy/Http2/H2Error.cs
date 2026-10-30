namespace ProxyMapService.Proxy.Http2
{
    public static class H2Error
    {
        public const uint NoError = 0, ProtocolError = 1, InternalError = 2, FlowControlError = 3, SettingsTimeout = 4,
            StreamClosed = 5, FrameSizeError = 6, RefusedStream = 7, Cancel = 8, CompressionError = 9;
    }
}
