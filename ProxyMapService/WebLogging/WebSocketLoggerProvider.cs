using Microsoft.Extensions.Options;

namespace ProxyMapService.WebLogging
{
    [ProviderAlias("WebSocket")]
    public class WebSocketLoggerProvider : ILoggerProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<LoggerFilterOptions> _filterOptions;
        private readonly IOptionsMonitor<WebSocketLoggerOptions> _loggerOptions;

        public WebSocketLoggerProvider(
            IServiceProvider serviceProvider,
            IOptionsMonitor<LoggerFilterOptions> filterOptions,
            IOptionsMonitor<WebSocketLoggerOptions> loggerOptions)
        {
            _serviceProvider = serviceProvider;
            _filterOptions = filterOptions;
            _loggerOptions = loggerOptions;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new WebSocketLogger(categoryName, _serviceProvider, _filterOptions, _loggerOptions);
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
