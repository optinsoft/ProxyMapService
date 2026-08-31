using Fare;
using ProxyMapService.Proxy.Configurations;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

namespace ProxyMapService.Proxy.Sessions
{
    public class SessionManager : ISessionManager
    {
        private string? _currentSessionId = null;
        private int? _currentSessionTime = null;
        private DateTime? _currentSessionExpiresAt = null;
        private UsernameParameterList? _currentSessionUsernameParameters = null;
        private readonly object _lock = new();

        private static readonly string _defaultSessionIdPattern = "^[A-Za-z]{8}";

        private readonly ConcurrentDictionary<string, (WebSocket Socket, Regex Filter)> _urlSubscriptions = new();
        private readonly Channel<(string SessionId, string Method, string Url)> _eventChannel;

        private readonly ILogger _logger;

        public SessionManager(ILogger logger)
        {
            _logger = logger;

            _eventChannel = Channel.CreateUnbounded<(string, string, string)>(new UnboundedChannelOptions
            {
                SingleReader = true
            });
        }

        public string? CurrentSessionId
        {
            get
            {
                string? id = null;
                lock (_lock)
                {
                    if (!IsCurrentSessionExpired(DateTime.Now))
                    {
                        id = _currentSessionId;
                    }
                }
                return id;
            }
        }

        public int? CurrentSessionTime
        {
            get
            {
                int? sessionTime = null;
                lock (_lock)
                {
                    if (!IsCurrentSessionExpired(DateTime.Now))
                    {
                        sessionTime = _currentSessionTime;
                    }
                }
                return sessionTime;
            }
        }

        public DateTime? CurrentSessionExpiresAt
        {
            get
            {
                DateTime? expiresAt = null;
                lock (_lock)
                {
                    if (!IsCurrentSessionExpired(DateTime.Now))
                    {
                        expiresAt = _currentSessionExpiresAt;
                    }
                }
                return expiresAt;
            }
        }

        public SessionInfo CurrentSessionInfo 
        {
            get
            {
                lock (_lock)
                {
                    bool expired = IsCurrentSessionExpired(DateTime.Now);
                    SessionInfo info = new()
                    {
                        SessionId = expired ? null : _currentSessionId,
                        SessionTime = expired ? null : _currentSessionTime,
                        ExpiresAt = expired ? null : _currentSessionExpiresAt,
                        UsernameParameters = expired ? null : _currentSessionUsernameParameters?.ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase)
                    };
                    return info;
                }
            }
        }

        public string? GetUsernameWithParameters(SessionContext context, string? username, UsernameParameterList? parameterList)
        {
            if (!string.IsNullOrEmpty(username) && parameterList != null)
            {
                foreach (var p in parameterList)
                {
                    string? value = ResolveParameterValue(context, p, DateTime.Now);
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (p.Name != "account")
                        {
                            username += $"-{p.Name}-{value}";
                        }
                    }
                }
            }
            return username;
        }

        public void PopulateContext(SessionContext context)
        {
            var now = DateTime.Now;
            lock (_lock)
            {
                if (!IsCurrentSessionExpired(now))
                {
                    context.SessionTime = _currentSessionTime ?? context.Mapping.Listen.StickyProxyLifetime;
                    if (context.UsernameParameters == null)
                    {
                        if (_currentSessionUsernameParameters != null)
                        {
                            context.UsernameParameters = new(_currentSessionUsernameParameters.Select(p => p.Clone()));
                        }
                    }
                }
                else
                {
                    context.SessionTime = context.Mapping.Listen.StickyProxyLifetime;
                }
            }
            if (context.Mapping.Authentication.SetAuthentication)
            {
                // Resolve SessionTime first (before SessionId)
                ResolveSessionTime(context, now);
                ResolveSessionId(context, now);
            }
            else if (context.Mapping.Listen.StickyProxyLifetime > 0)
            {
                // Resolve SessionTime first (before SessionId)
                ResolveSessionTime(context, now);
                ResolveSessionId(context, now);
            }
            if (context.SessionId == null && context.SessionTime > 0)
            {
                context.SessionId = GenerateSessionId(context, _defaultSessionIdPattern, now);
            }
            if (context.Mapping.Authentication.SetAuthentication)
            {
                ResolveAuthenticationUserParameters(context, now);
            }
        }

        public void ResetSessionId()
        {
            lock (_lock)
            {
                _currentSessionId = string.Empty;
                _currentSessionTime = null;
                _currentSessionExpiresAt = null;
                _currentSessionUsernameParameters = null;
            }
        }

        public void AddOrUpdateUrlSubscription(string sessionId, WebSocket socket, string urlPattern)
        {
            var regex = new Regex(urlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _urlSubscriptions[sessionId] = (socket, regex);
        }

        public void RemoveUrlSubscription(string sessionId)
        {
            _urlSubscriptions.TryRemove(sessionId, out _);
        }

        public void NotifyIfRequestUrlMatches(SessionContext context, HttpRequestHeader requestHeader, bool isSecure)
        {
            if (!context.SessionAPI.Enabled || !context.SessionAPI.WebSocketsEnabled)
            {
                return;
            }

            var sessionId = !string.IsNullOrEmpty(context.SessionId) ? context.SessionId : $":{context.Mapping.Listen.Port}"; 
            var method = requestHeader.HTTPVerb;

            if ( /* string.IsNullOrEmpty(sessionId) || */ string.IsNullOrEmpty(method))
            {
                return;
            }

            if (!_urlSubscriptions.ContainsKey(sessionId))
            {
                return;
            }

            var interceptedUrl = GetHttpRequestUrl(context, requestHeader, isSecure);
            if (string.IsNullOrEmpty(interceptedUrl))
            {
                return;
            }

            _eventChannel.Writer.TryWrite((sessionId, method, interceptedUrl));
        }

        public async Task StartEventProcessingLoop(CancellationToken stoppingToken)
        {
            var reader = _eventChannel.Reader;

            try
            {
                while (await reader.WaitToReadAsync(stoppingToken))
                {
                    while (reader.TryRead(out var item))
                    {
                        try
                        {
                            await DistributeEventToWebSockets(item.SessionId, item.Method, item.Url, stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to distribute event for session {SessionId}", item.SessionId);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Event processing loop was stopped gracefully.");
            }
        }

        private async Task DistributeEventToWebSockets(string sessionId, string method, string url, CancellationToken stoppingToken)
        {
            if (!_urlSubscriptions.TryGetValue(sessionId, out var subscription))
            {
                return;
            }

            var (socket, filter) = subscription;

            if (socket.State == WebSocketState.Open && filter.IsMatch(url))
            {

                var serializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = false
                };

                var payload = JsonSerializer.Serialize(new
                {
                    event_type = "url_matched",
                    timestamp = DateTime.UtcNow,
                    session_id = sessionId,
                    method,
                    url
                }, serializerOptions);

                byte[] responseBytes = Encoding.UTF8.GetBytes(payload);

                try
                {
                    await socket.SendAsync(
                        new ArraySegment<byte>(responseBytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        stoppingToken
                    );
                }
                catch
                {
                    _urlSubscriptions.TryRemove(sessionId, out _);
                }
            }
        }

        private bool IsCurrentSessionExpired(DateTime now)
        {
            return _currentSessionExpiresAt != null && now >= _currentSessionExpiresAt;
        }

        private string GenerateSessionId(SessionContext context, string pattern, DateTime now)
        {
            var newId = GenerateValue(pattern);
            return UpdateCurrentSessionId(context, newId, now);
        }

        private string UpdateCurrentSessionId(SessionContext context, string newId, DateTime now)
        {
            if (newId.Length == 0)
            {
                return newId;
            }
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_currentSessionId))
                {
                    if (_currentSessionExpiresAt != null && now < _currentSessionExpiresAt)
                    {
                        return _currentSessionId;
                    }
                }
                _currentSessionId = newId;
                // context.SessionTime must be set (resolved) before generating session
                _currentSessionTime = context.SessionTime;
                _currentSessionExpiresAt = DateTime.Now.AddMinutes(context.SessionTime);
                _currentSessionUsernameParameters = context.UsernameParameters != null ? new(context.UsernameParameters.Select(p => p.Clone())) : null;
            }
            return newId;
        }

        private string? ResolveParameterValue(SessionContext context, UsernameParameter? parameter, DateTime now)
        {
            if (parameter == null)
            {
                return null;
            }
            string? value = parameter.Value;
            string? contextParamName = null;
            string? contextParamValue = null;
            if (value.StartsWith('$'))
            {
                contextParamName = value.Substring(1);
                contextParamValue = context.UsernameParameters?.GetValue(contextParamName);
                if (contextParamValue != null)
                {
                    if (parameter.SessionId)
                    {
                        value = UpdateCurrentSessionId(context, contextParamValue, now);
                    }
                    else
                    {
                        value = contextParamValue;
                    }
                }
                else 
                {
                    value = parameter.Default;
                }
            }
            if (contextParamValue == null)
            {
                if (value != null && value.StartsWith('^'))
                {
                    var pattern = value.Substring(1);
                    if (parameter.SessionId)
                    {
                        value = context.SessionId ?? GenerateSessionId(context, pattern, now);
                    }
                    else
                    {
                        value = GenerateValue(pattern);
                    }
                }
                if (contextParamName != null && value != null)
                {
                    context.UsernameParameters ??= new();
                    context.UsernameParameters.SetResolvedValue(contextParamName, value, parameter);
                }
            }
            if (value != null)
            {
                if (parameter.SessionId)
                {
                    context.SessionId = value;
                }
                if (parameter.SessionTime)
                {
                    if (int.TryParse(value, out var time))
                    {
                        context.SessionTime = time;
                    }
                }
            }
            return value;
        }

        private void ResolveSessionId(SessionContext context, DateTime now)
        {
            ResolveParameterValue(context, context.Mapping.Authentication.UsernameParameters.SessionId, now);
        }

        private void ResolveSessionTime(SessionContext context, DateTime now)
        {
            ResolveParameterValue(context, context.Mapping.Authentication.UsernameParameters.SessionTime, now);
        }

        private void ResolveAuthenticationUserParameters(SessionContext context, DateTime now)
        {
            foreach (var p in context.Mapping.Authentication.UsernameParameters)
            {
                if (!p.SessionTime && !p.SessionId) // Skip already resolved SessionTime and SessionId
                {
                    ResolveParameterValue(context, p, now);
                }
            }
        }

        private static string GenerateValue(string pattern)
        {
            var xeger = new Xeger(pattern);
            return xeger.Generate();
        }

        private static string? GetHttpRequestUrl(SessionContext context, HttpRequestHeader requestHeader, bool isSecure)
        {
            if (string.Equals(requestHeader.HTTPVerb, "CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (requestHeader.HTTPTarget != null && requestHeader.HTTPTarget.Contains("://"))
            {
                return requestHeader.HTTPTarget;
            }

            string scheme = isSecure ? "https" : "http";

            if (!string.IsNullOrEmpty(requestHeader.Host))
            {
                var builder = new UriBuilder
                {
                    Scheme = scheme,
                    Path = requestHeader.HTTPTargetPath ?? "/",
                    Host = requestHeader.Host,
                };

                return builder.Uri.ToString();
            }
            else
            {
                var host = requestHeader.HTTPTargetHost ?? context.Host;

                if (host == null)
                {
                    return requestHeader.HTTPTargetPath;
                }

                var builder = new UriBuilder
                {
                    Scheme = scheme,
                    Path = requestHeader.HTTPTargetPath ?? "/",
                    Host = host.Hostname,
                    Port = host.Port,
                };

                return builder.Uri.ToString();
            }
        }

    }
}
