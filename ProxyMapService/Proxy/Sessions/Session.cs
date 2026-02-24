using ProxyMapService.Proxy.Authenticator;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Providers;
using ProxyMapService.Proxy.Resolvers;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Sessions
{
    public class Session
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
            { HandleStep.Socks4Initialized, Socks4AuthenticationHandler.Instance() },
            { HandleStep.Socks4AuthenticationNotRequired, Socks4HostActionHandler.Instance() },
            { HandleStep.Socks4Authenticated, Socks4HostActionHandler.Instance() },
            { HandleStep.Socks4Bypass, Socks4BypassHandler.Instance() },
            { HandleStep.Socks4File, Socks4FileHandler.Instance() },
            { HandleStep.Socks4Proxy, Socks4ProxyHandler.Instance() },
            { HandleStep.Socks5Initialized, Socks5AuthenticationHandler.Instance() },
            { HandleStep.Socks5AuthenticationNotRequired, Socks5ConnectRequestHandler.Instance() },
            { HandleStep.Socks5UsernamePasswordAuthentication, Socks5UsernamePasswordHandler.Instance() },
            { HandleStep.Socks5Authenticated, Socks5ConnectRequestHandler.Instance() },
            { HandleStep.Socks5ConnectRequested, Socks5HostActionHandler.Instance() },
            { HandleStep.Socks5Proxy, Socks5ProxyHandler.Instance() },
            { HandleStep.Socks5Bypass, Socks5BypassHandler.Instance() },
            { HandleStep.Socks5File, Socks5FileHandler.Instance() },
            { HandleStep.Proxy, ProxyHandler.Instance() },
            { HandleStep.Tunnel, TunnelHandler.Instance() }
        };

        public static async Task Run(TcpClient incomingClient, ProxyMapping mapping, IProxyProvider proxyProvider, 
            IProxyAuthenticator proxyAuthenticator, IUsernameParameterResolver usernameParameterResolver, List<HostRule> hostRules, 
            string? userAgent, ISessionsCounter? sessionsCounter, IBytesReadCounter? outgoingReadCounter, IBytesSentCounter? outgoingSentCounter, 
            IBytesReadCounter? incomingReadCounter, IBytesSentCounter? incomingSentCounter, ILogger logger, CancellationToken token)
        {
            X509Certificate2? serverCertificate;
            try
            {
                serverCertificate = mapping.Listen.ServerCertificate;
            }
            catch (Exception ex)
            {
                logger.LogError("Error: {ErrorMessage}", ex.Message);
                incomingClient.Close();
                return;
            }
            using var context = new SessionContext(incomingClient, mapping, 
                mapping.Listen.Ssl, serverCertificate, proxyProvider, 
                proxyAuthenticator, usernameParameterResolver, hostRules, userAgent,
                sessionsCounter, outgoingReadCounter, outgoingSentCounter,
                incomingReadCounter, incomingSentCounter, logger, token);
            sessionsCounter?.OnSessionStarted(context);
            var step = HandleStep.Initialize;
            do
            {
                try
                {
                    logger.LogDebug("Run step: {step}", step);
                    step = await Handlers[step].Run(context);
                    logger.LogDebug("Next step: {step}", step);
                }
                catch (Exception ex)
                {
                    logger.LogError("Error: {ErrorMessage}", ex.Message);
                    step = HandleStep.Terminate;
                }
            } while (step != HandleStep.Terminate);
        }
    }
}
