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
    }
}
