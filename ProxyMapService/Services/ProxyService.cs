using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProxyMapService.Interfaces;
using ProxyMapService.Proxy;
using ProxyMapService.Proxy.Configurations;
using System.Net;

namespace ProxyMapService.Services
{
    public class ProxyService : BackgroundService, IProxyService
    {
        private readonly string _serviceInfo = $"Service created at {DateTime.Now}";
        private readonly List<Task> _tasks = [];
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

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
                    _tasks.Add(
                        new ProxyMapper(
                            mapping,
                            _logger
                        ).Start());
                }
            }
        }

        public string GetServiceInfo()
        {
            return _serviceInfo;
        }
    }
}
