using Microsoft.Extensions.Options;

namespace ProxyMapService.WebLogging
{
    [ProviderAlias("WebSocket")]
    public class WebSocketLoggerProvider(
        IServiceProvider serviceProvider,
        IOptionsMonitor<LoggerFilterOptions> filterOptions,
        IOptionsMonitor<WebSocketLoggerOptions> loggerOptions) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new WebSocketLogger(categoryName, serviceProvider, filterOptions, loggerOptions);
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
