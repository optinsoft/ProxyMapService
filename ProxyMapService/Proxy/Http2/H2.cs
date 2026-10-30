namespace ProxyMapService.Proxy.Http2
{
    public static class H2
    {
        public const byte Data = 0, Headers = 1, Priority = 2, RstStream = 3, Settings = 4,
            PushPromise = 5, Ping = 6, GoAway = 7, WindowUpdate = 8, Continuation = 9;
        public const byte EndStream = 0x1, Ack = 0x1, EndHeaders = 0x4, Padded = 0x8, PriorityFlag = 0x20;
        public static readonly byte[] Preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();
    }
}
