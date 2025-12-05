using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using System.Net;
using System.Net.Sockets;

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
            { HandleStep.Socks4Initialized, Socks4HostActionHandler.Instance() },
            { HandleStep.Socks4Bypass, Socks4BypassHandler.Instance() },
            { HandleStep.Socks4Proxy, Socks4ProxyHandler.Instance() },
            { HandleStep.Socks5Initialized, Socks5AuthenticationHandler.Instance() },
            { HandleStep.Socks5AuthenticationNotRequired, Socks5ConnectRequestHandler.Instance() },
            { HandleStep.Socks5UsernamePasswordAuthentication, Socks5UsernamePasswordHandler.Instance() },
            { HandleStep.Socks5Authenticated, Socks5ConnectRequestHandler.Instance() },
            { HandleStep.Socks5ConnectRequested, Socks5HostActionHandler.Instance() },
            { HandleStep.Socks5Proxy, Socks5ProxyHandler.Instance() },
            { HandleStep.Socks5Bypass, Socks5BypassHandler.Instance() },
            { HandleStep.Proxy, ProxyHandler.Instance() },
            { HandleStep.Tunnel, TunnelHandler.Instance() }
        };

        public static async Task Run(TcpClient client, ProxyMapping mapping, List<HostRule>? hostRules, string? userAgent,
            SessionsCounter? sessionsCounter, BytesReadCounter? remoteReadCounter, BytesSentCounter? remoteSentCounter, 
            ILogger logger, CancellationToken token)
        {
            using var context = new SessionContext(client, mapping, hostRules, 
                userAgent, sessionsCounter, remoteReadCounter, remoteSentCounter,
                logger, token);
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
