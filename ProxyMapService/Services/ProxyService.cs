using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Proxy.Network;
using ProxyMapService.Counters;
using ProxyMapService.Interfaces;
using ProxyMapService.Models;
using ProxyMapService.Proxy;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using System.Net;

namespace ProxyMapService.Services
{
    public class ProxyService : BackgroundService, IProxyService
    {
        private readonly string _serviceInfo = $"Service created at {DateTime.Now}";
        private readonly List<Task> _tasks = [];
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly SessionsCounter _sessionsCounter = new();
        private readonly HostsCounter? _hostsCounter = null;
        private readonly BytesReadCounter _readCounter = new();
        private readonly BytesSentCounter _sentCounter = new();

        public ProxyService(IConfiguration configuration, ILogger<ProxyService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            var HostStatsEnabled = _configuration.GetSection("HostStats")?.GetValue<bool>("Enabled") ?? false;
            if (HostStatsEnabled)
            {
                _hostsCounter = new();
                _sessionsCounter.HTTPRequestHandler += _hostsCounter.SessionHTTPRequest;
                var HostTrafficStatsEnabled = _configuration.GetSection("HostStats")?.GetValue<bool>("TrafficStats") ?? false;
                if (HostTrafficStatsEnabled)
                {
                    _readCounter.BytesReadHandler += _hostsCounter.SessionBytesRead;
                    _sentCounter.BytesSentHandler += _hostsCounter.SessionBytesSent;
                }
            }
            AddProxyMappingTasks();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(-1, stoppingToken);
        }

        private void AddProxyMappingTasks()
        {
            var proxyMappings = _configuration.GetSection("ProxyMappings").Get<List<ProxyMapping>>();
            if (proxyMappings != null)
            {
                foreach (var mapping in proxyMappings)
                {
                    _tasks.Add(new ProxyMapper().Start(
                        mapping, _sessionsCounter, _readCounter, _sentCounter, _logger));
                }
            }
        }

        public string GetServiceInfo()
        {
            return _serviceInfo;
        }

        public int GetSessionsCount()
        {
            return _sessionsCounter.Count;
        }

        public int GetAuthenticationNotRequired()
        {
            return _sessionsCounter.AuthenticationNotRequired;
        }
        
        public int GetAuthenticationRequired()
        {
            return _sessionsCounter.AuthenticationRequired;
        }
        
        public int GetAuthenticated()
        {
            return _sessionsCounter.Authenticated;
        }
        
        public int GetAuthenticationInvalid()
        {
            return _sessionsCounter.AuthenticationInvalid;
        }

        public int GetHttpRejected()
        {
            return _sessionsCounter.HttpRejected;
        }

        public int GetConnected()
        {
            return _sessionsCounter.Connected;
        }
        
        public int GetConnectionFailed()
        {
            return _sessionsCounter.ConnectionFailed;
        }

        public int GetHeaderFailed()
        {
            return _sessionsCounter.HeaderFailed;
        }

        public int GetHostFailed()
        {
            return _sessionsCounter.HostFailed;
        }

        public long GetTotalBytesRead()
        {
            return _readCounter.TotalBytesRead;
        }

        public long GetTotalBytesSent()
        {
            return _sentCounter.TotalBytesSent;
        }

        public IEnumerable<KeyValuePair<string, HostStats>>? GetHostStats()
        {
            if (_hostsCounter == null)
            {
                return [];
            }
            return _hostsCounter.GetHostStats();
        }
    }
}
