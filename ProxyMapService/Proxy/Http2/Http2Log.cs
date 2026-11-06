namespace ProxyMapService.Proxy.Http2
{
    internal static partial class Http2Log
    {
        [LoggerMessage(EventId = 1501, Level = LogLevel.Debug,
            Message = "[h2 stream {StreamId}] {Method} {Path} -> {Status}, request {RequestBytes} B, response {ResponseBytes} B, {ElapsedMs} ms")]
        public static partial void StreamCompleted(ILogger logger, int streamId, string? method, string? path, int status,
            long requestBytes, long responseBytes, long elapsedMs);

        [LoggerMessage(EventId = 1502, Level = LogLevel.Debug,
            Message = "[h2 stream {StreamId}] reset by {By}, HTTP/2 error code {ErrorCode}")]
        public static partial void StreamReset(ILogger logger, int streamId, string by, uint errorCode);

        [LoggerMessage(EventId = 1503, Level = LogLevel.Debug,
            Message = "[h2] GOAWAY from {By}: last stream {LastStreamId}, HTTP/2 error code {ErrorCode}")]
        public static partial void GoAway(ILogger logger, string by, int lastStreamId, uint errorCode);

        [LoggerMessage(EventId = 1504, Level = LogLevel.Warning,
            Message = "[h2] traffic analysis stopped for {Direction}: {ExceptionName}: {ErrorMessage}. Forwarding continues, this direction is no longer logged.")]
        public static partial void ObserverFailed(ILogger logger, string direction, string exceptionName, string errorMessage);

        [LoggerMessage(EventId = 1505, Level = LogLevel.Warning,
            Message = "[h2 stream {StreamId}] cannot build an HTTP header for logging: {ErrorMessage}")]
        public static partial void CannotBuildHeader(ILogger logger, int streamId, string errorMessage);

        [LoggerMessage(EventId = 1506, Level = LogLevel.Debug,
            Message = "[h2] client {Client}: header rewriting is enabled for {Host}")]
        public static partial void LogHttp2HostRewritingEnabled(this ILogger logger, System.Net.EndPoint? client, ProxyMapService.Proxy.Network.HostAddress host);

        [LoggerMessage(EventId = 1507, Level = LogLevel.Debug,
            Message = "[h2 stream {StreamId}] request host rewritten, :authority is now {Authority}")]
        public static partial void LogHttp2HostRewritten(this ILogger logger, int streamId, string? authority);

        [LoggerMessage(EventId = 1508, Level = LogLevel.Warning,
            Message = "[h2 stream {StreamId}] request handler failed: {ExceptionName}: {ErrorMessage}")]
        public static partial void ServerHandlerFailed(ILogger? logger, int streamId, string exceptionName, string errorMessage);

        [LoggerMessage(EventId = 1509, Level = LogLevel.Debug,
            Message = "[h2] server connection closed: {Reason}")]
        public static partial void ServerConnectionClosed(ILogger? logger, string reason);
    }
}
