using Microsoft.Extensions.Logging;
using ProxyMapService.Proxy.Authenticator;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Listeners;
using ProxyMapService.Proxy.Provider;
using ProxyMapService.Proxy.Sessions;
using System.Data;
using System.Net;
using System.Net.Sockets;

namespace ProxyMapService.Proxy
{
    public class ProxyMapper(ProxyMapping mapping, List<HostRule>? hostRules,
        string? userAgent, ISessionsCounter? sessionsCounter,
        IBytesReadCounter? remoteReadCounter, IBytesSentCounter? remoteSentCounter,
        IBytesReadCounter? clientReadCounter, IBytesSentCounter? clientSentCounter,
        ILogger logger, int maxListenerStartRetries, CancellationToken stoppingToken)
    {
        public async Task Start()
        {
            ProxyProvider proxyProvider = new(mapping.ProxyServers);
            ProxyAuthenticator proxyAuthenticator = new(mapping.Authentication);

            List<Task> tasks = [];

            for (int port = mapping.Listen.PortRange.Start; port <= mapping.Listen.PortRange.End; port++)
            {
                tasks.Add(StartListenerAsync(port, proxyProvider, proxyAuthenticator));
            }

            await Task.WhenAll(tasks); ;
        }

        private async Task StartListenerAsync(int listenPort, ProxyProvider proxyProvider, ProxyAuthenticator proxyAuthenticator)
        {
            var localEndPoint = new IPEndPoint(IPAddress.Loopback, listenPort);

            async void clientHandler(TcpClient client, CancellationToken token) =>
                await Session.Run(client, mapping, proxyProvider,
                    proxyAuthenticator, hostRules, userAgent,
                    sessionsCounter, remoteReadCounter, remoteSentCounter,
                    clientReadCounter, clientSentCounter,
                    logger, token);

            using var listener = new Listener(localEndPoint, clientHandler, logger);
            await listener.Start(maxListenerStartRetries, stoppingToken);
        }
    }
}
