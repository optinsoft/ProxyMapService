using Fare;
using Microsoft.Extensions.Caching.Memory;
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
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy
{
    public class ProxyMapper(ProxyMapping mapping, List<HostRule> hostRules, 
        string? userAgent, SslClientOptionsConfig sslClientConfig, SslServerOptionsConfig sslServerConfig,
        ISessionsCounter? sessionsCounter,
        IBytesReadCounter? outgoingReadCounter, IBytesSentCounter? outgoingSentCounter,
        IBytesReadCounter? incomingReadCounter, IBytesSentCounter? incomingSentCounter,
        IBytesReadCounter? incomingSslCounter, IBytesReadCounter? outgoingSslCounter,
        ILogger logger, bool logStep, int maxListenerStartRetries, CancellationToken stoppingToken)
    {
        private readonly List<ProxyServer> _proxyServers = [];
        private readonly List<IConfigurationRoot> _proxyServerFileConfigurations = [];

        private void LoadProxyServers()
        {
            _proxyServers.Clear();
            _proxyServers.AddRange(mapping.ProxyServers.Items);
            _proxyServerFileConfigurations.Clear();
            foreach (var proxyServersFile in mapping.ProxyServers.Files) 
            {
                var fileConfig = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(proxyServersFile.Path, optional: false)
                    .Build();
                _proxyServerFileConfigurations.Add(fileConfig);
            }
            foreach (var fileConfig in _proxyServerFileConfigurations)
            {
                var addProxyServers = fileConfig.GetSection("Items").Get<List<ProxyServer>>();
                if (addProxyServers != null)
                {
                    _proxyServers.AddRange(addProxyServers);
                }
            }
        }

        public async Task Start()
        {
            try
            {
                LoadProxyServers();

                ProxyProvider proxyProvider = new(_proxyServers);
                ProxyAuthenticator proxyAuthenticator = new(mapping.Authentication);

                List<Task> tasks = [];

                for (int port = mapping.Listen.PortRange.Start; port <= mapping.Listen.PortRange.End; port++)
                {
                    if (port > 0 && port < 65536)
                    {
                        tasks.Add(StartListenerAsync(port, proxyProvider, proxyAuthenticator));
                    }
                    else
                    {
                        logger.LogWarning("Bad listen port: {}", port);
                    }
                }

                await Task.WhenAll(tasks); ;
            } 
            catch (Exception ex)
            {
                logger.LogError(ex, "ProxyMapper.Start failed with exception");
            }
        }

        private async Task StartListenerAsync(int listenPort, ProxyProvider proxyProvider, ProxyAuthenticator proxyAuthenticator)
        {
            var incomingEndPoint = new IPEndPoint(IPAddress.Loopback, listenPort);

            UsernameParameterResolver usernameParameterResolver = new();

            async void incomingClientHandler(TcpClient client, CancellationToken token) =>
                await Session.Run(client, mapping, proxyProvider,
                    proxyAuthenticator, usernameParameterResolver,
                    hostRules, userAgent, sslClientConfig, sslServerConfig,
                    sessionsCounter, outgoingReadCounter, outgoingSentCounter,
                    incomingReadCounter, incomingSentCounter,
                    incomingSslCounter, outgoingSslCounter,
                    logger, logStep, token);

            using var listener = new Listener(incomingEndPoint, incomingClientHandler, logger);
            await listener.Start(maxListenerStartRetries, stoppingToken);
        }
    }
}
