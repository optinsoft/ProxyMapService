using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Socks;
using System.Net.Sockets;

namespace ProxyMapService.Proxy.Handlers
{
    public static partial class HandlerLogExtensions
    {
        [LoggerMessage(
            EventId = 1201,
            Level = LogLevel.Information,
            Message = "Client connected from {remoteEndPoint}")]
        private static partial void LogClientConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1202,
            Level = LogLevel.Information,
            Message = "Connected to server {remoteEndPoint} (bypass)")]
        private static partial void LogBypassServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1202,
            Level = LogLevel.Information,
            Message = "Connected to server {hostname}:{port} ({remoteEndPoint}) (bypass)")]
        private static partial void LogBypassServerConnectedInternal2(this ILogger logger, string hostname, int port, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1203,
            Level = LogLevel.Information,
            Message = "Connected to http proxy server {remoteEndPoint}")]
        private static partial void LogHttpProxyServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1203,
            Level = LogLevel.Information,
            Message = "Connected to http proxy server {hostname}:{port} ({remoteEndPoint})")]
        private static partial void LogHttpProxyServerConnectedInternal2(this ILogger logger, string hostname, int port, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1204,
            Level = LogLevel.Information,
            Message = "Connected to socks4 proxy server {remoteEndPoint}")]
        private static partial void LogSocks4ProxyServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1204,
            Level = LogLevel.Information,
            Message = "Connected to socks4 proxy server {hostname}:{port} ({remoteEndPoint})")]
        private static partial void LogSocks4ProxyServerConnectedInternal2(this ILogger logger, string hostname, int port, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1205,
            Level = LogLevel.Information,
            Message = "Connected to socks5 proxy server {remoteEndPoint}")]
        private static partial void LogSocks5ProxyServerConnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1205,
            Level = LogLevel.Information,
            Message = "Connected to socks5 proxy server {hostname}:{port} ({remoteEndPoint})")]
        private static partial void LogSocks5ProxyServerConnectedInternal2(this ILogger logger, string hostname, int port, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1206,
            Level = LogLevel.Information,
            Message = "Connected to server {hostname}:{port}")]
        private static partial void LogServerConnectedInternal(this ILogger logger, string hostname, int port);

        [LoggerMessage(
            EventId = 1207,
            Level = LogLevel.Information,
            Message = "Connected to server {hostname}:{port} via http proxy {proxyHost}:{proxyPort}")]
        private static partial void LogServerConnectedViaHttpProxy(this ILogger logger, string hostname, int port, string proxyHost, int proxyPort);

        [LoggerMessage(
            EventId = 1208,
            Level = LogLevel.Information,
            Message = "Connected to server {hostname}:{port} via socks4 proxy {proxyHost}:{proxyPort}")]
        private static partial void LogServerConnectedViaSocks4Proxy(this ILogger logger, string hostname, int port, string proxyHost, int proxyPort);

        [LoggerMessage(
            EventId = 1209,
            Level = LogLevel.Information,
            Message = "Connected to server {hostname}:{port} via socks5 proxy {proxyHost}:{proxyPort}")]
        private static partial void LogServerConnectedViaSocks5Proxy(this ILogger logger, string hostname, int port, string proxyHost, int proxyPort);

        [LoggerMessage(
            EventId = 1210,
            Level = LogLevel.Warning,
            Message = "Connection to proxy server {ipEndPoint} failed. {message}")]
        private static partial void LogProxyServerConnectionFailedInternal(this ILogger logger, string message, System.Net.IPEndPoint ipEndPoint);

        [LoggerMessage(
            EventId = 1210,
            Level = LogLevel.Warning,
            Message = "Connection to proxy server {hostname}:{port} ({ipEndPoint}) failed. {message}")]
        private static partial void LogProxyServerConnectionFailedInternal2(this ILogger logger, string message, string hostname, int port, System.Net.IPEndPoint ipEndPoint);

        [LoggerMessage(
            EventId = 1211,
            Level = LogLevel.Warning,
            Message = "HTTP connection to server {hostname}:{port} failed. {message}")]
        private static partial void LogHttpConnectionFailedInternal(this ILogger logger, string message, string hostname, int port);

        [LoggerMessage(
            EventId = 1211,
            Level = LogLevel.Warning,
            Message = "HTTP connection to server {hostname}:{port} via {proxyHost}:{proxyPort} failed. {message}")]
        private static partial void LogHttpConnectionFailedInternal2(this ILogger logger, string message, string hostname, int port, string proxyHost, int proxyPort);

        [LoggerMessage(
            EventId = 1212,
            Level = LogLevel.Warning,
            Message = "SOCKS4 connection to server {hostname}:{port} failed. {message}")]
        private static partial void LogSocks4ConnectionFailedInternal(this ILogger logger, string message, string hostname, int port);

        [LoggerMessage(
            EventId = 1212,
            Level = LogLevel.Warning,
            Message = "SOCKS4 connection to server {hostname}:{port} via {proxyHost}:{proxyPort} failed. {message}")]
        private static partial void LogSocks4ConnectionFailedInternal2(this ILogger logger, string message, string hostname, int port, string proxyHost, int proxyPort);

        [LoggerMessage(
            EventId = 1213,
            Level = LogLevel.Warning,
            Message = "SOCKS5 connection to server {hostname}:{port} failed. {message}")]
        private static partial void LogSocks5ConnectionFailedInternal(this ILogger logger, string message, string hostname, int port);

        [LoggerMessage(
            EventId = 1213,
            Level = LogLevel.Warning,
            Message = "SOCKS5 connection to server {hostname}:{port} via {proxyHost}:{proxyPort} failed. {message}")]
        private static partial void LogSocks5ConnectionFailedInternal2(this ILogger logger, string message, string hostname, int port, string proxyHost, int proxyPort);

        [LoggerMessage(
            EventId = 1221,
            Level = LogLevel.Information,
            Message = "Client disconnected. Address: {remoteEndPoint}")]
        private static partial void LogClientDisconnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1222,
            Level = LogLevel.Information,
            Message = "Server disconnected (bypass). Address: {remoteEndPoint}")]
        private static partial void LogBypassServerDisconnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1223,
            Level = LogLevel.Information,
            Message = "Proxy server disconnected. Address: {remoteEndPoint}")]
        private static partial void LogProxyServerDisconnectedInternal(this ILogger logger, System.Net.EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventId = 1231,
            Level = LogLevel.Information,
            Message = "Response served from file: {filePath}")]
        public static partial void LogResponseFromFile(this ILogger logger, string filePath);

        [LoggerMessage(
            EventId = 1232,
            Level = LogLevel.Information,
            Message = "Response served from cache: {filePath}")]
        public static partial void LogResponseFromCache(this ILogger logger, string filePath);

        [LoggerMessage(
            EventId = 1241,
            Level = LogLevel.Warning,
            Message = "Host lookup failed: {HostName}. {ErrorMessage}")]
        public static partial void LogHostError(this ILogger logger, string errorMessage, string hostName);

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

        public static void LogBypassServerConnected(this ILogger logger, TcpClient outgoingClient, HostAddress host)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            if (host.IsHostnameIP())
            {
                logger.LogBypassServerConnectedInternal(remoteEndPoint);
            }
            else
            {
                logger.LogBypassServerConnectedInternal2(host.Hostname, host.Port, remoteEndPoint);
            }
        }

        public static void LogHttpProxyServerConnected(this ILogger logger, TcpClient outgoingClient, ProxyServer proxyServer)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            if (HostAddress.IsHostnameIP(proxyServer.Host))
            {
                logger.LogHttpProxyServerConnectedInternal(remoteEndPoint);
            }
            else
            {
                logger.LogHttpProxyServerConnectedInternal2(proxyServer.Host, proxyServer.Port, remoteEndPoint);
            }
        }

        public static void LogSocks4ProxyServerConnected(this ILogger logger, TcpClient outgoingClient, ProxyServer proxyServer)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            if (HostAddress.IsHostnameIP(proxyServer.Host))
            {
                logger.LogSocks4ProxyServerConnectedInternal(remoteEndPoint);
            }
            else
            {
                logger.LogSocks4ProxyServerConnectedInternal2(proxyServer.Host, proxyServer.Port, remoteEndPoint);
            }
        }

        public static void LogSocks5ProxyServerConnected(this ILogger logger, TcpClient outgoingClient, ProxyServer proxyServer)
        {
            var remoteEndPoint = GetTcpClientRemoteEndPoint(outgoingClient);
            if (HostAddress.IsHostnameIP(proxyServer.Host))
            {
                logger.LogSocks5ProxyServerConnectedInternal(remoteEndPoint);
            }
            else
            {
                logger.LogSocks5ProxyServerConnectedInternal2(proxyServer.Host, proxyServer.Port, remoteEndPoint);
            }
        }

        public static void LogServerConnectedViaHttpProxy(this ILogger logger, HostAddress host, ProxyServer? proxyServer)
        {
            if (proxyServer != null)
            {
                logger.LogServerConnectedViaHttpProxy(host.Hostname, host.Port, proxyServer.Host, proxyServer.Port);
            }
            else
            {
                logger.LogServerConnectedInternal(host.Hostname, host.Port);
            }
        }

        public static void LogServerConnectedViaSocks4Proxy(this ILogger logger, HostAddress host, ProxyServer? proxyServer)
        {
            if (proxyServer != null)
            {
                logger.LogServerConnectedViaSocks4Proxy(host.Hostname, host.Port, proxyServer.Host, proxyServer.Port);
            }
            else
            {
                logger.LogServerConnectedInternal(host.Hostname, host.Port);
            }
        }

        public static void LogServerConnectedViaSocks5Proxy(this ILogger logger, HostAddress host, ProxyServer? proxyServer)
        {
            if (proxyServer != null)
            {
                logger.LogServerConnectedViaSocks5Proxy(host.Hostname, host.Port, proxyServer.Host, proxyServer.Port);
            }
            else
            {
                logger.LogServerConnectedInternal(host.Hostname, host.Port);
            }
        }

        public static void LogProxyServerConnectionFailed(this ILogger logger, string message, System.Net.IPEndPoint ipEndPoint, ProxyServer proxyServer)
        {
            if (ipEndPoint.Address.IsIPv4MappedToIPv6)
            {
                ipEndPoint = new System.Net.IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
            }
            if (HostAddress.IsHostnameIP(proxyServer.Host))
            {
                logger.LogProxyServerConnectionFailedInternal(message, ipEndPoint);
            }
            else
            {
                logger.LogProxyServerConnectionFailedInternal2(message, proxyServer.Host, proxyServer.Port, ipEndPoint);
            }
        }

        public static void LogHttpConnectionFailed(this ILogger logger, HttpResponseHeader? responseHttp, HostAddress host, ProxyServer? proxyServer)
        {
            string message = responseHttp != null ? $"{responseHttp.StatusCode} {responseHttp.StatusText}" : "response it empty";
            if (proxyServer != null)
            {
                logger.LogHttpConnectionFailedInternal2(message, host.Hostname, host.Port, proxyServer.Host, proxyServer.Port);
            }
            else
            {
                logger.LogHttpConnectionFailedInternal(message, host.Hostname, host.Port);
            }
        }

        public static void LogSocks4ConnectionFailed(this ILogger logger, Socks4Command command, HostAddress host, ProxyServer? proxyServer)
        {
            string message = command.ToLogMessage();
            if (proxyServer != null)
            {
                logger.LogSocks4ConnectionFailedInternal2(message, host.Hostname, host.Port, proxyServer.Host, proxyServer.Port);
            }
            else
            {
                logger.LogSocks4ConnectionFailedInternal(message, host.Hostname, host.Port);
            }
        }

        public static void LogSocks5ConnectionFailed(this ILogger logger, Socks5Status status, HostAddress host, ProxyServer? proxyServer)
        {
            string message = status.ToLogMessage();
            if (proxyServer != null)
            {
                logger.LogSocks5ConnectionFailedInternal2(message, host.Hostname, host.Port, proxyServer.Host, proxyServer.Port);
            }
            else
            {
                logger.LogSocks5ConnectionFailedInternal(message, host.Hostname, host.Port);
            }
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
