using System.Text;
using ProxyMapService.Proxy.Handlers;

namespace ProxyMapService.Proxy.Tls
{
    /// <summary>
    /// Diagnostics logging for the TLS record splitter, gated on
    /// <see cref="HandlerLogExtensions.TlsHandshakeLogEnabled"/> (DetailedLogging:LogTlsHandshake).
    /// </summary>
    internal static partial class TlsRecordSplitLog
    {
        [LoggerMessage(
            EventId = 1318,
            Level = LogLevel.Debug,
            Message = "TLS record split: SNI '{Sni}' ({SniLength} B at record offset {SniStart}..{SniEnd}) -> {RecordCount} record(s): {Layout}")]
        private static partial void LogSniSplitInternal(this ILogger logger, string sni, int sniLength, int sniStart, int sniEnd, int recordCount, string layout);

        /// <summary>
        /// Logs how the SNI hostname of the ClientHello is distributed over the resulting
        /// TLS records after the split. <paramref name="sniStart"/>/<paramref name="sniEnd"/>
        /// are record-relative offsets of the hostname (5-byte record header included),
        /// <paramref name="segments"/> are the records produced by the splitter.
        /// </summary>
        public static void LogSniSplit(ILogger? logger, string sni, int sniStart, int sniEnd, List<byte[]> segments)
        {
            if (logger == null || !HandlerLogExtensions.TlsHandshakeLogEnabled) return;
            logger.LogSniSplitInternal(sni, sniEnd - sniStart, sniStart, sniEnd, segments.Count, DescribeLayout(sni, sniStart, sniEnd, segments));
        }

        private static string DescribeLayout(string sni, int sniStart, int sniEnd, List<byte[]> segments)
        {
            var sb = new StringBuilder();
            int payload = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                int len = segments[i].Length - 5;
                int segStart = 5 + payload;   // record-relative coordinates (header included)
                int segEnd = segStart + len;
                sb.Append('#').Append(i + 1).Append(' ').Append(len).Append(" B");

                // Part of the SNI hostname that falls into this record, in SNI-relative coordinates
                int from = Math.Max(sniStart, segStart) - segStart;
                int to = Math.Min(sniEnd, segEnd) - segStart;
                if (to > from)
                {
                    sb.Append(" [SNI ").Append(from).Append("..").Append(to)
                      .Append(" '").Append(sni[from..to]).Append("']");
                }
                payload += len;
            }
            return sb.ToString();
        }
    }
}
