using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Tunnels
{
    public partial class HttpTunnel
    {
        protected readonly SessionContext _context;
        protected readonly CountingStream _source;
        protected readonly CountingStream _destination;
        protected readonly TaskCompletionSource _destinationReady;
        protected readonly BytesReadCounter _readCounter;
        protected readonly BytesSendCounter _sendCounter;
        protected readonly TunnelState _selfState;
        protected readonly TunnelState _otherTunnelState;

        protected const int BufferSize = 8192;

        public HttpTunnel(
            SessionContext context,
            CountingStream source,
            CountingStream destination,
            TaskCompletionSource destinationReady,
            BytesReadCounter readCounter,
            BytesSendCounter sendCounter,
            TunnelState selfState,
            TunnelState otherTunnelState)
        {
            _context = context;
            _source = source;
            _destination = destination;
            _destinationReady = destinationReady;
            _readCounter = readCounter;
            _sendCounter = sendCounter;
            _selfState = selfState;
            _otherTunnelState = otherTunnelState;
        }

        #region High-Performance Logging

        [LoggerMessage(
            EventId = 1401,
            Level = LogLevel.Error,
            Message = "[Tunnel] {ExceptionName}: {ErrorMessage}\n{StackTrace}")]
        protected static partial void LogTunnelError(ILogger logger, string exceptionName, string errorMessage, string? stackTrace);

        [LoggerMessage(
            EventId = 1402,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: reading from {direction}...")]
        protected static partial void LogTunnelReading(ILogger logger, long tunnelId, string direction);

        [LoggerMessage(
            EventId = 1403,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Reset reading headers")]
        protected static partial void LogTunnelResetReadingHeaders(ILogger logger, long tunnelId);

        [LoggerMessage(
            EventId = 1404,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Resetting other tunnel ({otherTunnelId}) reading headers")]
        protected static partial void LogOtherTunnelResetReadingHeaders(ILogger logger, long tunnelId, long otherTunnelId);

        [LoggerMessage(
            EventId = 1405,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Reading headers from {direction}...")]
        protected static partial void LogTunnelReadingHeaders(ILogger logger, long tunnelId, string direction);

        [LoggerMessage(
            EventId = 1406,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Headers read from {direction}")]
        protected static partial void LogTunnelHeadersRead(ILogger logger, long tunnelId, string direction);

        [LoggerMessage(
            EventId = 1407,
            Level = LogLevel.Debug,
            Message = "[Tunnel] {ExceptionName}: {ErrorMessage}")]
        protected static partial void LogTunnelDebugError(ILogger logger, string exceptionName, string errorMessage);

        [LoggerMessage(
            EventId = 1408,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: Body read from {direction}")]
        protected static partial void LogTunnelBodyRead(ILogger logger, long tunnelId, string direction);

        [LoggerMessage(
            EventId = 1409,
            Level = LogLevel.Debug,
            Message = "Tunnel {tunnelId}: sending to {direction}...")]
        protected static partial void LogTunnelSending(ILogger logger, long tunnelId, string direction);

        #endregion

        protected static async Task SendModifiedHeadersAndBody(Stream destination, string[] headerLines, byte[]? bodyBytes, CancellationToken token)
        {
            string modifiedHeaders = string.Join("\r\n", headerLines);
            byte[] modifiedHeaderBytes = Encoding.ASCII.GetBytes(modifiedHeaders);
            await destination.WriteAsync(modifiedHeaderBytes.AsMemory(0, modifiedHeaderBytes.Length), token);
            if (bodyBytes?.Length > 0)
            {
                await destination.WriteAsync(bodyBytes.AsMemory(0, bodyBytes.Length), token);
            }
        }

        protected static async Task ReplyCacheFile(CacheEntry cacheEntry, FileStream cacheFileStream, CountingStream incomingStream, SessionContext context)
        {
            context.ProxyCounters.SessionsCounter?.OnCacheResponse(context);
            await HttpProto.HttpReplyCacheFileStream(context, incomingStream, cacheEntry, cacheFileStream);
            context.Logger.LogResponseFromCache(cacheFileStream.Name);
        }
    }
}
