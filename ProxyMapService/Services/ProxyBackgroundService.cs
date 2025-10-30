using ProxyMapService.Interfaces;
using System.Threading;

namespace ProxyMapService.Services
{
    public class ProxyBackgroundService(IProxyService proxyService, ILogger<ProxyService> logger) : BackgroundService
    {
        private readonly IProxyService _proxyService = proxyService;
        private readonly ILogger _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[BackgroundService] Service is ready to start its work.");

            try
            {
                _proxyService.StoppingToken = stoppingToken;

                _proxyService.StartProxyMappingTasks();
                
                _logger.LogInformation("[BackgroundService] Waiting for complete...");
                await Task.Delay(Timeout.Infinite, stoppingToken);

                _logger.LogInformation("[BackgroundService] Completed.");

            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("[BackgroundService] Stopped by Host Shutdown.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BackgroundService] An unexpected error occurred.");
            }
        }
    }
}
