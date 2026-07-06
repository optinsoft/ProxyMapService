using Microsoft.Extensions.Options;
using ProxyMapService.Services;

namespace ProxyMapService.WebLogging
{
    public class WebSocketLogger(
        string categoryName,
        IServiceProvider serviceProvider,
        IOptionsMonitor<LoggerFilterOptions> filterOptions,
        IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions) : ILogger
    {
        private WebSocketLogBackgroundService? _backgroundService;
        private IEventLoggingSwitch? _loggingSwitch;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            var currentOptions = monitoringOptions.CurrentValue;
            if (!currentOptions.EventLog.Enabled)
            {
                return false;
            }

            _loggingSwitch ??= serviceProvider.GetRequiredService<IEventLoggingSwitch>();

            if (!_loggingSwitch.IsEventCapture) return false;

            var filter = filterOptions.CurrentValue;
            if (categoryName.StartsWith("Microsoft.AspNetCore.SignalR") ||
                categoryName.StartsWith("Microsoft.AspNetCore.Routing"))
            {
                return false;
            }

            var rules = filter.Rules;
            LogLevel? minLogLevel = null;

            minLogLevel ??= FindRule(rules, "WebSocket", categoryName);

            minLogLevel ??= FindRule(rules, "WebSocket", null);

            minLogLevel ??= FindRule(rules, null, categoryName);

            minLogLevel ??= FindRule(rules, null, null);

            return logLevel >= (minLogLevel ?? LogLevel.Information);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            _backgroundService ??= serviceProvider.GetRequiredService<WebSocketLogBackgroundService>();

            var message = formatter(state, exception);

            var logEntry = new LogMessageEntry(
                Timestamp: DateTime.UtcNow,
                Category: categoryName,
                Level: logLevel.ToString(),
                Message: message,
                Exception: exception?.Message
            );
            
            _backgroundService.QueueMessage(logEntry);
        }

        private static LogLevel? FindRule(IEnumerable<LoggerFilterRule> rules, string? provider, string? category)
        {
            foreach (var rule in rules)
            {
                if (rule.ProviderName == provider)
                {
                    if (category == null && rule.CategoryName == null)
                        return rule.LogLevel;

                    if (category != null && rule.CategoryName != null && category.StartsWith(rule.CategoryName))
                        return rule.LogLevel;
                }
            }
            return null;
        }
    }
}
