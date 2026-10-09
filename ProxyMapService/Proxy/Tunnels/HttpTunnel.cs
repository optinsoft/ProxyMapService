using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Tunnels
{
    public abstract partial class HttpTunnel : IDisposable
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public async Task RunAsync()
        {
            var buffer = new byte[BufferSize];

            var token = _context.TunnelToken;

            try
            {
                int bytesRead;
                do
                {
                    if (_readCounter.IsLogReading)
                    {
                        LogTunnelReading(
                            _context.Logger,
                            _selfState.TunnelId,
                            StreamDirectionName.GetName(_readCounter.Direction));
                    }
                    try
                    {
                        bytesRead = await _source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    }
                    catch (IOException ex)
                    {
                        if (_readCounter.IsLogReading)
                        {
                            LogTunnelDebugError(_context.Logger, ex.GetType().Name, ex.Message);
                        }
                        _source.OnDisconnected();
                        break;
                    }
                    if (bytesRead > 0)
                    {
                        await ProcessAsync(buffer.AsMemory(0, bytesRead), token);
                    }
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (_context.TunnelToken.IsCancellationRequested)
            {
                // Normal tunnel shutdown.
            }
            catch (ObjectDisposedException ex)
            {
                if (_readCounter.IsLogReading)
                {
                    LogTunnelDebugError(_context.Logger, ex.GetType().Name, ex.Message);
                }
            }
            catch (IOException ex)
            {
                if (_readCounter.IsLogReading)
                {
                    LogTunnelDebugError(_context.Logger, ex.GetType().Name, ex.Message);
                }
            }
            catch (Exception ex)
            {
                LogTunnelError(_context.Logger, ex.GetType().Name, ex.Message, ex.StackTrace);
            }
        }

        protected abstract Task ProcessAsync(ReadOnlyMemory<byte> data, CancellationToken token);

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
