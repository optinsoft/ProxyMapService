using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProxyMapService.Interfaces;
using ProxyMapService.Proxy;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
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
        private readonly BytesReadCounter _readCounter = new();
        private readonly BytesSentCounter _sentCounter = new();

        public ProxyService(IConfiguration configuration, ILogger<ProxyService> logger)
        {
            _configuration = configuration;
            _logger = logger;
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

        public long GetTotalBytesRead()
        {
            return _readCounter.TotalBytesRead;
        }

        public long GetTotalBytesSent()
        {
            return _sentCounter.TotalBytesSent;
        }
    }
}
