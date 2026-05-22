using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ProxyMapService.WebLogging
{
    public class WebSocketLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<LoggerFilterOptions> _filterOptions;
        private readonly IOptionsMonitor<WebSocketLoggerOptions> _loggerOptions;

        public WebSocketLogger(
            string categoryName,
            IServiceProvider serviceProvider,
            IOptionsMonitor<LoggerFilterOptions> filterOptions,
            IOptionsMonitor<WebSocketLoggerOptions> loggerOptions)
        {
            _categoryName = categoryName;
            _serviceProvider = serviceProvider;
            _filterOptions = filterOptions;
            _loggerOptions = loggerOptions;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (!_loggerOptions.CurrentValue.Enabled) return false;

            var filter = _filterOptions.CurrentValue;
            if (_categoryName.StartsWith("Microsoft.AspNetCore.SignalR") ||
                _categoryName.StartsWith("Microsoft.AspNetCore.Routing"))
            {
                return false;
            }

            var rules = filter.Rules;
            LogLevel? minLogLevel = null;

            minLogLevel ??= FindRule(rules, "WebSocket", _categoryName);

            minLogLevel ??= FindRule(rules, "WebSocket", null);

            minLogLevel ??= FindRule(rules, null, _categoryName);

            minLogLevel ??= FindRule(rules, null, null);

            return logLevel >= (minLogLevel ?? LogLevel.Information);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);

            var logEntry = new
            {
                Timestamp = DateTime.UtcNow,
                Category = _categoryName,
                Level = logLevel.ToString(),
                Message = message,
                Exception = exception?.Message
            };

            var hubContext = _serviceProvider.GetService<IHubContext<LogHub>>();

            if (hubContext != null)
            {
                _ = hubContext.Clients.All.SendAsync("EventLog", logEntry);
            }
        }

        private LogLevel? FindRule(System.Collections.Generic.IEnumerable<LoggerFilterRule> rules, string? provider, string? category)
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
