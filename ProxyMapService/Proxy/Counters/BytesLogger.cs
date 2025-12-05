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
            var logData = bytesData == null ? "null" : String.Join("\r\n", Enumerable.Range(0, (bytesRead + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesRead - i * 16, 16))
            ));
            _logger.LogInformation("<<< read {count} byte(s):\r\n{data}", bytesRead, logData);
        }

        public void LogBytesSent(object? sender, BytesSentEventArgs e)
        {
            var bytesSent = e.BytesSent;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var logData = bytesData == null ? "null" : String.Join("\r\n", Enumerable.Range(0, (bytesSent + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesSent - i * 16, 16))
            ));
            _logger.LogInformation(">>> sent {count} byte(s):\r\n{data}", bytesSent, logData);
        }

        private static string FormatLogData(byte[] data, int startIndex, int length)
        {
            return BitConverter.ToString(data, startIndex, length).Replace("-", " ").PadRight(48, ' ') + " " +
                string.Concat(Encoding.ASCII.GetString(data, startIndex, length).Select(c => c < 32 ? '.' : c));
        }

    }
}
