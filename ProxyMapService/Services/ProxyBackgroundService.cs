using ProxyMapService.Interfaces;

namespace ProxyMapService.Services
{
    public class ProxyBackgroundService(IProxyService proxyService, ILogger<ProxyService> logger) : BackgroundService
    {
        private readonly IProxyService _proxyService = proxyService;
        private readonly ILogger _logger = logger;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ProxyBackgroundService] adding proxy mapping tasks...");
            _proxyService.AddProxyMappingTasks(stoppingToken);
            _logger.LogInformation("[ProxyBackgroundService] waiting for complete...");
            await Task.Delay(-1, stoppingToken);
            _logger.LogInformation("[ProxyBackgroundService] completed.");
        }
    }
}
