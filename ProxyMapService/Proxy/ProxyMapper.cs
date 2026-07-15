using Microsoft.Extensions.Caching.Memory;
using ProxyMapService.Proxy.Authenticator;
using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Listeners;
using ProxyMapService.Proxy.Providers;
using ProxyMapService.Proxy.Resolvers;
using ProxyMapService.Proxy.Sessions;
using System.Net.Sockets;

namespace ProxyMapService.Proxy
{
    public class ProxyMapper(ProxyMapping mapping, List<PortRange> listenPorts,
        SessionAPIConfig sessionAPI, List<HostRule> hostRules, 
        List<CacheRule> cacheRules, CacheManager cacheManager, string? userAgent, 
        SslClientOptionsConfig sslClientConfig, SslServerOptionsConfig sslServerConfig,
        ProxyCounters proxyCounters, ILogger serviceLogger, ILogger sessionLogger, 
        bool logStep, int maxListenerStartRetries, CancellationToken stoppingToken)
    {
        private readonly List<ProxyServer> _proxyServers = [];
        private readonly List<IConfigurationRoot> _proxyServerFileConfigurations = [];

        private List<CacheRule>? _cacheRules = null;

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

                if (mapping.CacheRules != null)
                {
                    _cacheRules = [];
                    CacheRules.LoadRulesList(_cacheRules, mapping.CacheRules);
                }
                else
                {
                    _cacheRules = null;
                }

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
                        serviceLogger.LogWarning("Bad listen port: {}", port);
                    }
                }

                if (tasks.Count > 0)
                {
                    listenPorts.Add(mapping.Listen.PortRange);
                    try
                    {
                        await Task.WhenAll(tasks);
                    }
                    finally
                    {
                        listenPorts.Remove(mapping.Listen.PortRange);
                    }
                }
            } 
            catch (Exception ex)
            {
                serviceLogger.LogError(ex, "ProxyMapper.Start failed with exception");
            }
        }

        private async Task StartListenerAsync(int listenPort, ProxyProvider proxyProvider, ProxyAuthenticator proxyAuthenticator)
        {
            var inboundEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, listenPort);

            UsernameParameterResolver usernameParameterResolver = new();

            async void incomingClientHandler(TcpClient client, CancellationToken token) =>
                await Session.Run(inboundEndPoint, client, mapping, sessionAPI,
                    proxyProvider, proxyAuthenticator, usernameParameterResolver,
                    hostRules, _cacheRules ?? cacheRules, cacheManager, userAgent, 
                    sslClientConfig, sslServerConfig,
                    proxyCounters, sessionLogger, logStep, token);

            using var listener = new Listener(inboundEndPoint, incomingClientHandler, serviceLogger);
            await listener.Start(maxListenerStartRetries, stoppingToken);
        }
    }
}
