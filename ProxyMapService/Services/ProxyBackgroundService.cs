using ProxyMapService.Interfaces;

namespace ProxyMapService.Services
{
    public class ProxyBackgroundService(IProxyService proxyService) : BackgroundService
    {
        private readonly IProxyService _proxyService = proxyService;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(-1, stoppingToken);
        }
    }
}
