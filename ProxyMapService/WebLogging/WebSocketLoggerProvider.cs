using Microsoft.Extensions.Options;
using ProxyMapService.Services;

namespace ProxyMapService.WebLogging
{
    [ProviderAlias("WebSocket")]
    public class WebSocketLoggerProvider(
        IServiceProvider serviceProvider,
        IOptionsMonitor<LoggerFilterOptions> filterOptions,
        IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new WebSocketLogger(categoryName, serviceProvider, filterOptions, monitoringOptions);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;
        }
    }
}
