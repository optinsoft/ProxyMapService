using Microsoft.Extensions.Logging;
using ProxyMapService.Proxy.Authenticator;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Listeners;
using ProxyMapService.Proxy.Providers;
using ProxyMapService.Proxy.Resolvers;
using ProxyMapService.Proxy.Sessions;
using System.Data;
using System.Net;
using System.Net.Sockets;

namespace ProxyMapService.Proxy
{
    public class ProxyMapper(ProxyMapping mapping, List<HostRule> hostRules, 
        string? userAgent, ISessionsCounter? sessionsCounter,
        IBytesReadCounter? remoteReadCounter, IBytesSentCounter? remoteSentCounter,
        IBytesReadCounter? clientReadCounter, IBytesSentCounter? clientSentCounter,
        ILogger logger, int maxListenerStartRetries, CancellationToken stoppingToken)
    {
        private readonly List<ProxyServer> _proxyServers = [];
        private readonly List<IConfigurationRoot> _proxyServerFileConfigurations = [];

        private void LoadProxyServers()
        {
            _proxyServers.Clear();
            _proxyServers.AddRange(mapping.ProxyServers);
            _proxyServerFileConfigurations.Clear();
            foreach (var proxyServersFile in mapping.ProxyServersFiles) 
            {
                var fileConfig = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(proxyServersFile.Path, optional: false)
                    .Build();
                _proxyServerFileConfigurations.Add(fileConfig);
            }
            foreach (var fileConfig in _proxyServerFileConfigurations)
            {
                var addProxyServers = fileConfig.GetSection("ProxyServers").Get<List<ProxyServer>>();
                if (addProxyServers != null)
                {
                    _proxyServers.AddRange(addProxyServers);
                }
            }
        }

        public async Task Start()
        {
            LoadProxyServers();

            ProxyProvider proxyProvider = new(_proxyServers);
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

            UsernameParameterResolver usernameParameterResolver = new();

            async void clientHandler(TcpClient client, CancellationToken token) =>
                await Session.Run(client, mapping, proxyProvider,
                    proxyAuthenticator, usernameParameterResolver,
                    hostRules, userAgent,
                    sessionsCounter, remoteReadCounter, remoteSentCounter,
                    clientReadCounter, clientSentCounter,
                    logger, token);

            using var listener = new Listener(localEndPoint, clientHandler, logger);
            await listener.Start(maxListenerStartRetries, stoppingToken);
        }
    }
}
