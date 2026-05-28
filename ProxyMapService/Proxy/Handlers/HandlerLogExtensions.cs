namespace ProxyMapService.Proxy.Handlers
{
    public static partial class HandlerLogExtensions
    {
        [LoggerMessage(
            EventId = 1201,
            Level = LogLevel.Information,
            Message = "Connected to server (bypass). Address: {remoteEndPoint}")]
        private static partial void LogBypassServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1202,
            Level = LogLevel.Information,
            Message = "Connected to http proxy server. Address: {remoteEndPoint}")]
        private static partial void LogHttpProxyServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1203,
            Level = LogLevel.Information,
            Message = "Connected to socks4 proxy server. Address: {remoteEndPoint}")]
        private static partial void LogSocks4ProxyServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1204,
            Level = LogLevel.Information,
            Message = "Connected to socks5 proxy server. Address: {remoteEndPoint}")]
        private static partial void LogSocks5ProxyServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1205,
            Level = LogLevel.Information,
            Message = "Client disconnected. Address: {remoteEndPoint}")]
        private static partial void LogClientDisconnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        public static void LogBypassServerConnected(this ILogger logger, System.Net.EndPoint? remoteEndPoint)
        {
            if (remoteEndPoint is System.Net.IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address.IsIPv4MappedToIPv6)
                {
                    remoteEndPoint = new System.Net.IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                }
            }
            logger.LogBypassServerConnectedInternal(remoteEndPoint);
        }

        public static void LogHttpProxyServerConnected(this ILogger logger, System.Net.EndPoint? remoteEndPoint)
        {
            if (remoteEndPoint is System.Net.IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address.IsIPv4MappedToIPv6)
                {
                    remoteEndPoint = new System.Net.IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                }
            }
            logger.LogHttpProxyServerConnectedInternal(remoteEndPoint);
        }

        public static void LogSocks4ProxyServerConnected(this ILogger logger, System.Net.EndPoint? remoteEndPoint)
        {
            if (remoteEndPoint is System.Net.IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address.IsIPv4MappedToIPv6)
                {
                    remoteEndPoint = new System.Net.IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                }
            }
            logger.LogSocks4ProxyServerConnectedInternal(remoteEndPoint);
        }

        public static void LogSocks5ProxyServerConnected(this ILogger logger, System.Net.EndPoint? remoteEndPoint)
        {
            if (remoteEndPoint is System.Net.IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address.IsIPv4MappedToIPv6)
                {
                    remoteEndPoint = new System.Net.IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                }
            }
            logger.LogSocks5ProxyServerConnectedInternal(remoteEndPoint);
        }

        public static void LogClientDisconnected(this ILogger logger, System.Net.EndPoint? remoteEndPoint)
        {
            if (remoteEndPoint is System.Net.IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address.IsIPv4MappedToIPv6)
                {
                    remoteEndPoint = new System.Net.IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                }
            }
            logger.LogClientDisconnectedInternal(remoteEndPoint);
        }
    }
}
