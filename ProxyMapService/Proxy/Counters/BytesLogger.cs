namespace ProxyMapService.Proxy.Counters
{
    public partial class BytesLogger(ILogger logger)
    {
        #region LoggerMessage Generators (Compile-time generated high-performance methods)

        [LoggerMessage(
            EventId = 1003,
            Level = LogLevel.Debug,
            Message = "{Arrows}{Tunnel} read {Count} byte(s) from the {Direction}:\r\n{Data}")]
        private static partial void LogBytesReadInternal(ILogger logger, string arrows, string tunnel, int count, string direction, string data);

        [LoggerMessage(
            EventId = 1004,
            Level = LogLevel.Debug,
            Message = "{Arrows}{Tunnel} sent {Count} byte(s) to the {Direction}:\r\n{Data}")]
        private static partial void LogBytesSentInternal(ILogger logger, string arrows, string tunnel, int count, string direction, string data);

        [LoggerMessage(
            EventId = 1005,
            Level = LogLevel.Debug,
            Message = "{Arrows}{Tunnel} decoded {Count} byte(s) from the {Direction}:\r\n{Data}")]
        private static partial void LogSslBytesDecodedInternal(ILogger logger, string arrows, string tunnel, int count, string direction, string data);

        [LoggerMessage(
            EventId = 1006,
            Level = LogLevel.Debug,
            Message = "{Arrows}{Tunnel} encoded {Count} byte(s) to the {Direction}:\r\n{Data}")]
        private static partial void LogSslBytesEncodedInternal(ILogger logger, string arrows, string tunnel, int count, string direction, string data);

        [LoggerMessage(
            EventId = 1007,
            Level = LogLevel.Debug,
            Message = "\r\n{Headers}")]
        private static partial void LogHttpHeadersInternal(ILogger logger, string headers);

        #endregion

        #region Public Methods

        public void LogBytesRead(object? sender, BytesReadEventArgs e)
        {
            // Guard clause: prevents CPU cycles and string allocations if Debug level is disabled
            if (!logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            var bytesRead = e.BytesRead;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var direction = e.Direction;

            var tunnel = $" [tunnel {e.TunnelId}]";

            // Hex formatting executes only when Debug logging is actively running
            var logData = bytesData == null ? "null" : string.Join("\r\n", Enumerable.Range(0, (bytesRead + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesRead - i * 16, 16))
            ));

            LogBytesReadInternal(logger,
                StreamDirectionArrows.GetReadArrows(direction),
                tunnel,
                bytesRead,
                StreamDirectionName.GetName(direction),
                logData);
        }

        public void LogBytesSent(object? sender, BytesSentEventArgs e)
        {
            // Guard clause: prevents CPU cycles and string allocations if Debug level is disabled
            if (!logger.IsEnabled(LogLevel.Debug)) return;

            var bytesSent = e.BytesSent;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var direction = e.Direction;
            var tunnel = $" [tunnel {e.TunnelId}]";

            // Hex formatting executes only when Debug logging is actively running
            var logData = bytesData == null ? "null" : string.Join("\r\n", Enumerable.Range(0, (bytesSent + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesSent - i * 16, 16))
            ));

            LogBytesSentInternal(logger,
                StreamDirectionArrows.GetSentArrows(direction),
                tunnel,
                bytesSent,
                StreamDirectionName.GetName(direction),
                logData);
        }

        public void LogSslBytesDecoded(object? sender, BytesReadEventArgs e)
        {
            // Guard clause: prevents CPU cycles and string allocations if Debug level is disabled
            if (!logger.IsEnabled(LogLevel.Debug)) return;

            var bytesRead = e.BytesRead;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var direction = e.Direction;
            var tunnel = $" [tunnel {e.TunnelId}]";

            var logData = bytesData == null ? "null" : string.Join("\r\n", Enumerable.Range(0, (bytesRead + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesRead - i * 16, 16))
            ));

            LogSslBytesDecodedInternal(logger,
                StreamDirectionArrows.GetReadArrows(direction),
                tunnel,
                bytesRead,
                StreamDirectionName.GetName(direction),
                logData);
        }

        public void LogSslBytesEncoded(object? sender, BytesSendEventArgs e)
        {
            // Guard clause: prevents CPU cycles and string allocations if Debug level is disabled
            if (!logger.IsEnabled(LogLevel.Debug)) return;

            var bytesSent = e.BytesSend;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var direction = e.Direction;
            var tunnel = $" [tunnel {e.TunnelId}]";

            var logData = bytesData == null ? "null" : string.Join("\r\n", Enumerable.Range(0, (bytesSent + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesSent - i * 16, 16))
            ));

            LogSslBytesEncodedInternal(logger,
                StreamDirectionArrows.GetSentArrows(direction),
                tunnel,
                bytesSent,
                StreamDirectionName.GetName(direction),
                logData);
        }

        public void LogHttpHeaders(object? sender, HttpHeadersEventArgs e)
        {
            // Guard clause: avoids string.Join memory overhead if Debug logging is turned off
            if (!logger.IsEnabled(LogLevel.Debug)) return;

            var headers = e.Headers;
            var logHeaders = headers == null ? "null" : string.Join("\r\n", headers);

            LogHttpHeadersInternal(logger, logHeaders);
        }

        #endregion

        #region High-Performance Hex Formatter

        /// <summary>
        /// Formats a byte array segment into a classic 65-character Hex + ASCII memory dump line.
        /// Uses Zero-Allocation Span-based formatting inside string.Create.
        /// </summary>
        private static string FormatLogData(byte[] data, int startIndex, int length)
        {
            if (data == null || length <= 0) return string.Empty;

            // Fixed length budget: 48 chars (Hex) + 1 char (Space separator) + 16 chars (ASCII representation)
            const int totalLength = 65;

            // Allocates exactly 1 object in the managed heap (the final string itself)
            return string.Create(totalLength, (data, startIndex, length), (span, state) =>
            {
                // 1. Initialize the entire buffer with spaces (handles default padding)
                span.Fill(' ');

                var (array, start, len) = state;
                ReadOnlySpan<char> hexChars = "0123456789ABCDEF";

                for (int i = 0; i < len; i++)
                {
                    byte b = array[start + i];

                    // 2. Write Hex characters directly into their pre-calculated positions
                    int hexPos = i * 3;
                    span[hexPos] = hexChars[b >> 4];     // High nibble
                    span[hexPos + 1] = hexChars[b & 0xF]; // Low nibble
                                                          // span[hexPos + 2] remains a space character due to span.Fill(' ') above

                    // 3. Write ASCII representation directly at the end section of the span
                    int asciiPos = 49 + i;
                    // Filter out control and non-printable characters
                    span[asciiPos] = (b < 32 || b > 126) ? '.' : (char)b;
                }
            });
        }

        #endregion
    }
}
