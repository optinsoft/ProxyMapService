using Microsoft.AspNetCore.SignalR;
using ProxyMapService.Interfaces;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Utils;
using ProxyMapService.WebLogging;

namespace ProxyMapService.Services
{
    public class ProxyBackgroundService : BackgroundService
    {
        private readonly IProxyService _proxyService;
        private readonly ILogger _logger;
        private readonly IHubContext<LogHub> _hubContext;
        private readonly IEventLoggingSwitch _eventLoggingSwitch;
        private readonly IHttpLoggingSwitch _httpLoggingSwitch;
        private readonly string _serviceId = RandomStringGenerator.GenerateRandomString(6);

        public ProxyBackgroundService(IProxyService proxyService, ILogger<ProxyService> logger, IHubContext<LogHub> hubContext,
            IEventLoggingSwitch eventLoggingSwitch, IHttpLoggingSwitch httpLoggingSwitch)
        {
            _proxyService = proxyService;
            _logger = logger;
            _hubContext = hubContext;
            _eventLoggingSwitch = eventLoggingSwitch;
            _httpLoggingSwitch = httpLoggingSwitch;
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

                //await Task.Delay(Timeout.Infinite, stoppingToken);
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        var stats = new StatsMessageEntry(
                            ServiceInfo: _proxyService.GetServiceInfo(),
                            Started: _proxyService.Started,
                            StartTime: _proxyService.GetStartTime(),
                            StopTime: _proxyService.GetStopTime(),
                            CurrentTime: _proxyService.GetCurrentTime(),
                            SessionsCount: _proxyService.GetSessionsCount(),
                            AuthenticationNotRequired: _proxyService.GetAuthenticationNotRequired(),
                            AuthenticationRequired: _proxyService.GetAuthenticationRequired(),
                            Authenticated: _proxyService.GetAuthenticated(),
                            AuthenticationInvalid: _proxyService.GetAuthenticationInvalid(),
                            HttpRejected: _proxyService.GetHttpRejected(),
                            HeaderFailed: _proxyService.GetHeaderFailed(),
                            NoHost: _proxyService.GetNoHost(),
                            HostRejected: _proxyService.GetHostRejected(),
                            HostProxified: _proxyService.GetHostProxified(),
                            HostBypassed: _proxyService.GetHostBypassed(),
                            ProxyConnected: _proxyService.GetProxyConnected(),
                            ProxyFailed: _proxyService.GetProxyFailed(),
                            BypassConnected: _proxyService.GetBypassConnected(),
                            BypassFailed: _proxyService.GetBypassFailed(),
                            TotalBytesRead: _proxyService.GetTotalBytesRead(),
                            TotalBytesSent: _proxyService.GetTotalBytesSent(),
                            ProxyBytesRead: _proxyService.GetProxyBytesRead(),
                            ProxyBytesSent: _proxyService.GetProxyBytesSent(),
                            BypassBytesRead: _proxyService.GetBypassBytesRead(),
                            BypassBytesSent: _proxyService.GetBypassBytesSent(),
                            CacheResponses: _proxyService.GetCacheResponses(),
                            CacheBytesSent: _proxyService.GetCacheBytesSent(),
                            LogCapture: _eventLoggingSwitch.IsEventCapture,
                            HttpCapture: _httpLoggingSwitch.IsHttpCapture
                        );

                        await _hubContext.Clients.All.SendAsync("Stats", stats, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[BackgroundService.{}] Error sending stats at {}.",
                            _serviceId,
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    }
                }

                _logger.LogInformation(
                    "[BackgroundService.{}] Completed at {}.",
                    _serviceId,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));

            }
            catch (OperationCanceledException)
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
