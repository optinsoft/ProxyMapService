using System.Text;

namespace ProxyMapService.Proxy.Counters
{
    public class BytesLogger (ILogger logger)
    {
        private readonly ILogger _logger = logger;

        public void LogBytesRead(object? sender, BytesReadEventArgs e)
        {
            var bytesRead = e.BytesRead;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var direction = e.Direction;
            var tunnel = $" [tunnel {e.TunnelId}]";
            var logData = bytesData == null ? "null" : String.Join("\r\n", Enumerable.Range(0, (bytesRead + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesRead - i * 16, 16))
            ));
            _logger.LogInformation("{arrows}{tunnel} read {count} byte(s) from the {direction}:\r\n{data}", 
                StreamDirectionArrows.GetReadArrows(direction), 
                tunnel,
                bytesRead, 
                StreamDirectionName.GetName(direction), 
                logData);
        }

        public void LogBytesSent(object? sender, BytesSentEventArgs e)
        {
            var bytesSent = e.BytesSent;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var direction = e.Direction;
            var tunnel = $" [tunnel {e.TunnelId}]";
            var logData = bytesData == null ? "null" : String.Join("\r\n", Enumerable.Range(0, (bytesSent + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesSent - i * 16, 16))
            ));
            _logger.LogInformation("{arrows}{tunnel} sent {count} byte(s) to the {direction}:\r\n{data}", 
                StreamDirectionArrows.GetSentArrows(direction),
                tunnel,
                bytesSent, 
                StreamDirectionName.GetName(direction), 
                logData);
        }

        public void LogSslBytesDecoded(object? sender, BytesReadEventArgs e)
        {
            var bytesRead = e.BytesRead;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var direction = e.Direction;
            var tunnel = $" [tunnel {e.TunnelId}]";
            var logData = bytesData == null ? "null" : String.Join("\r\n", Enumerable.Range(0, (bytesRead + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesRead - i * 16, 16))
            ));
            _logger.LogInformation("{arrows}{tunnel} decoded {count} byte(s) from the {direction}:\r\n{data}", 
                StreamDirectionArrows.GetReadArrows(direction),
                tunnel,
                bytesRead, 
                StreamDirectionName.GetName(direction), 
                logData);
        }

        public void LogSslBytesEncoded(object? sender, BytesSendEventArgs e)
        {
            var bytesSent = e.BytesSend;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var direction = e.Direction;
            var tunnel = $" [tunnel {e.TunnelId}]";
            var logData = bytesData == null ? "null" : String.Join("\r\n", Enumerable.Range(0, (bytesSent + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesSent - i * 16, 16))
            ));
            _logger.LogInformation("{arrows}{tunnel} encoded {count} byte(s) to the {direction}:\r\n{data}", 
                StreamDirectionArrows.GetSentArrows(direction),
                tunnel,
                bytesSent, 
                StreamDirectionName.GetName(direction), 
                logData);
        }

        public void LogHttpHeaders(object? sender, HttpHeadersEventArgs e)
        {
            var headers = e.Headers;
            var logHeaders = headers == null ? "null" : String.Join("\r\n", headers);
            _logger.LogInformation("\r\n{headers}",
                logHeaders);
        }

        private static string FormatLogData(byte[] data, int startIndex, int length)
        {
            return BitConverter.ToString(data, startIndex, length).Replace("-", " ").PadRight(48, ' ') + " " +
                string.Concat(Encoding.ASCII.GetString(data, startIndex, length).Select(c => c < 32 ? '.' : c));
        }
    }
}
