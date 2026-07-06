using Microsoft.VisualBasic;
using ProxyMapService.Proxy.Authenticator;
using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Providers;
using ProxyMapService.Proxy.Resolvers;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Sessions
{
    public partial class Session
    {
        private static readonly Dictionary<HandleStep, IHandler> Handlers = new()
        {
            { HandleStep.Initialize, InitializeHandler.Instance() },
            { HandleStep.HttpInitialized, HttpAuthenticationHandler.Instance() },
            { HandleStep.HttpAuthenticationNotRequired, HttpHostActionHandler.Instance() },
            { HandleStep.HttpAuthenticated, HttpHostActionHandler.Instance() },
            { HandleStep.HttpProxy, HttpProxyHandler.Instance() },
            { HandleStep.HttpBypass, HttpBypassHandler.Instance() },
            { HandleStep.HttpFile, HttpFileHandler.Instance() },
            { HandleStep.HttpSessionAPI, HttpSessionAPIHandler.Instance() },
            { HandleStep.Socks4Initialized, Socks4AuthenticationHandler.Instance() },
            { HandleStep.Socks4AuthenticationNotRequired, Socks4HostActionHandler.Instance() },
            { HandleStep.Socks4Authenticated, Socks4HostActionHandler.Instance() },
            { HandleStep.Socks4Bypass, Socks4BypassHandler.Instance() },
            { HandleStep.Socks4File, Socks4FileHandler.Instance() },
            { HandleStep.Socks4Proxy, Socks4ProxyHandler.Instance() },
            { HandleStep.Socks4SessionAPI, Socks4SessionAPIHandler.Instance() },
            { HandleStep.Socks5Initialized, Socks5AuthenticationHandler.Instance() },
            { HandleStep.Socks5AuthenticationNotRequired, Socks5ConnectRequestHandler.Instance() },
            { HandleStep.Socks5UsernamePasswordAuthentication, Socks5UsernamePasswordHandler.Instance() },
            { HandleStep.Socks5Authenticated, Socks5ConnectRequestHandler.Instance() },
            { HandleStep.Socks5ConnectRequested, Socks5HostActionHandler.Instance() },
            { HandleStep.Socks5Proxy, Socks5ProxyHandler.Instance() },
            { HandleStep.Socks5Bypass, Socks5BypassHandler.Instance() },
            { HandleStep.Socks5File, Socks5FileHandler.Instance() },
            { HandleStep.Socks5SessionAPI, Socks5SessionAPIHandler.Instance() },
            { HandleStep.Proxy, ProxyHandler.Instance() },
            { HandleStep.Tunnel, TunnelHandler.Instance() },
            { HandleStep.HandleFileRequest, FileRequestHandler.Instance() },
            { HandleStep.HandleSessionAPI, SessionAPIHandler.Instance() },
        };

        #region High-Performance Logging

        [LoggerMessage(
            EventId = 1101,
            Level = LogLevel.Error,
            Message = "Server Certificate Error: {ErrorMessage}")]
        private static partial void LogCertificateError(ILogger logger, string errorMessage);

        [LoggerMessage(
            EventId = 1102,
            Level = LogLevel.Error,
            Message = "CA Certificate Error: {ErrorMessage}")]
        private static partial void LogCACertificateError(ILogger logger, string errorMessage);

        [LoggerMessage(
            EventId = 1103,
            Level = LogLevel.Debug,
            Message = "Run step: {step}")]
        private static partial void LogRunStep(ILogger logger, HandleStep step);

        [LoggerMessage(
            EventId = 1104,
            Level = LogLevel.Debug,
            Message = "Next step: {step}")]
        private static partial void LogNextStep(ILogger logger, HandleStep step);

        #endregion

        public static async Task Run(System.Net.IPEndPoint inboundEndpoint, TcpClient incomingClient, 
            ProxyMapping mapping, SessionAPIConfig sessionAPI, IProxyProvider proxyProvider, IProxyAuthenticator proxyAuthenticator, 
            IUsernameParameterResolver usernameParameterResolver, List<HostRule> hostRules, 
            List<CacheRule> cacheRules, CacheManager cacheManager, string? userAgent, 
            SslClientOptionsConfig sslClientConfig, SslServerOptionsConfig sslServerConfig,
            ProxyCounters proxyCounters, ILogger logger, bool logStep, CancellationToken token)
        {
            var incomingEndPoint = incomingClient.Client.RemoteEndPoint;
            if (incomingEndPoint is System.Net.IPEndPoint ipEndPoint)
            {
                if (ipEndPoint.Address.IsIPv4MappedToIPv6)
                {
                    incomingEndPoint = new System.Net.IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
                }
            }

            logger.LogClientConnected(incomingEndPoint);

            X509Certificate2? serverCertificate, caCertificate;
            try
            {
                serverCertificate = sslServerConfig.ServerCertificate.Certificate;
            }
            catch (Exception ex)
            {
                LogCertificateError(logger, ex.Message);
                incomingClient.Close();
                return;
            }
            try
            {
                caCertificate = sslServerConfig.CACertificate.Certificate;
            }
            catch (Exception ex)
            {
                LogCACertificateError(logger, ex.Message);
                incomingClient.Close();
                return;
            }
            try
            {
                using var context = new SessionContext(
                    inboundEndpoint, incomingClient, incomingEndPoint, 
                    mapping, sessionAPI, serverCertificate, caCertificate,
                    proxyProvider, proxyAuthenticator, usernameParameterResolver,
                    hostRules, cacheRules, cacheManager, userAgent,
                    sslClientConfig, sslServerConfig,
                    proxyCounters, logger, token);
                proxyCounters.SessionsCounter?.OnSessionStarted(context);
                var step = HandleStep.Initialize;
                do
                {
                    try
                    {
                        if (logStep)
                        {
                            LogRunStep(logger, step);
                        }
                        step = await Handlers[step].Run(context);
                        if (logStep)
                        {
                            LogNextStep(logger, step);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error: {ErrorMessage}", ex.Message);
                        step = HandleStep.Terminate;
                    }
                } while (step != HandleStep.Terminate);
                context.CompletionLogger?.OnHttpCompleted(context);
            }
            finally
            {
                incomingClient.Close();
            }
        }
    }
}
