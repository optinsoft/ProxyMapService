using ProxyMapService.Proxy.Sessions;
using System.Net.Sockets;

namespace ProxyMapService.Proxy.Handlers
{
    public static partial class HandlerLogExtensions
    {
        [LoggerMessage(
            EventId = 1201,
            Level = LogLevel.Information,
            Message = "Client connected. Address: {remoteEndPoint}")]
        private static partial void LogClientConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1202,
            Level = LogLevel.Information,
            Message = "Connected to server (bypass). Address: {remoteEndPoint}")]
        private static partial void LogBypassServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1203,
            Level = LogLevel.Information,
            Message = "Connected to http proxy server. Address: {remoteEndPoint}")]
        private static partial void LogHttpProxyServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1204,
            Level = LogLevel.Information,
            Message = "Connected to socks4 proxy server. Address: {remoteEndPoint}")]
        private static partial void LogSocks4ProxyServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1205,
            Level = LogLevel.Information,
            Message = "Connected to socks5 proxy server. Address: {remoteEndPoint}")]
        private static partial void LogSocks5ProxyServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1206,
            Level = LogLevel.Information,
            Message = "Client disconnected. Address: {remoteEndPoint}")]
        private static partial void LogClientDisconnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1207,
            Level = LogLevel.Information,
            Message = "Server disconnected (bypass). Address: {remoteEndPoint}")]
        private static partial void LogBypassServerDisconnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1208,
            Level = LogLevel.Information,
            Message = "Proxy server disconnected. Address: {remoteEndPoint}")]
        private static partial void LogProxyServerDisconnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        private static System.Net.EndPoint? GetTcpClientRemoteEndPoint(TcpClient client)
        {
            var remoteEndPoint = client.Client.RemoteEndPoint;
            if (remoteEndPoint is System.Net.IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address.IsIPv4MappedToIPv6)
                {
                    return new System.Net.IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                }
            }
            return remoteEndPoint;
        }

        public static void LogClientConnected(this ILogger logger, TcpClient incomingClient)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(incomingClient);
            logger.LogClientConnectedInternal(remoteEndPoint);
        }

        public static void LogBypassServerConnected(this ILogger logger, TcpClient outgoingClient)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            logger.LogBypassServerConnectedInternal(remoteEndPoint);
        }

        public static void LogHttpProxyServerConnected(this ILogger logger, TcpClient outgoingClient)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            logger.LogHttpProxyServerConnectedInternal(remoteEndPoint);
        }

        public static void LogSocks4ProxyServerConnected(this ILogger logger, TcpClient outgoingClient)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            logger.LogSocks4ProxyServerConnectedInternal(remoteEndPoint);
        }

        public static void LogSocks5ProxyServerConnected(this ILogger logger, TcpClient outgoingClient)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            logger.LogSocks5ProxyServerConnectedInternal(remoteEndPoint);
        }

        public static void LogClientDisconnected(this ILogger logger, TcpClient incomingClient)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(incomingClient);
            logger.LogClientDisconnectedInternal(remoteEndPoint);
        }

        public static void LogBypassServerDisconnected(this ILogger logger, TcpClient outgoingClient)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            logger.LogBypassServerDisconnectedInternal(remoteEndPoint);
        }

        public static void LogProxyServerDisconnected(this ILogger logger, TcpClient outgoingClient)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            logger.LogProxyServerDisconnectedInternal(remoteEndPoint);
        }
    }
}
