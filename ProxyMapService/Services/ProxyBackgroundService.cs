using ProxyMapService.Interfaces;
using ProxyMapService.Utils;
using System.Threading;

namespace ProxyMapService.Services
{
    public class ProxyBackgroundService : BackgroundService
    {
        private readonly IProxyService _proxyService;
        private readonly ILogger _logger;
        private string _serviceId = RandomStringGenerator.GenerateRandomString(6);

        public ProxyBackgroundService(IProxyService proxyService, ILogger<ProxyService> logger)
        {
            _proxyService = proxyService;
            _logger = logger;
            _logger.LogInformation(
                "[BackgroundService.{}] Service is created at {}.",
                _serviceId,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[BackgroundService.{}] Service is ready to start its work at {}.",
                _serviceId,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

            try
            {
                _proxyService.StoppingToken = stoppingToken;

                _proxyService.StartProxyMappingTasks();
                
                _logger.LogInformation(
                    "[BackgroundService.{}] Waiting for complete at {}...",
                    _serviceId,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                await Task.Delay(Timeout.Infinite, stoppingToken);

                _logger.LogInformation(
                    "[BackgroundService.{}] Completed at {}.",
                    _serviceId,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation(
                    "[BackgroundService.{}] Stopped by Host Shutdown at {}.",
                    _serviceId,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "[BackgroundService.{}] An unexpected error occurred at {}.",
                    _serviceId,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            }
        }
    }
}
