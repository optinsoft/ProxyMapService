using Fare;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Ws;
using System;
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
        private string? _currentUserAgent = null;
        private bool _currentUserAgentExplicit = false;
        private readonly object _lock = new();

        private static readonly string _defaultSessionIdPattern = "^[A-Za-z]{8}";
        private sealed record Subscription(string Id, WsConnection Connection, Regex Filter, string SessionId);

        private readonly object _subLock = new();
        private readonly Dictionary<string, Subscription> _byId = new();
        private volatile Dictionary<string, Subscription[]> _bySession = new();
        //private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
        private const int MaxSubscriptionsPerConnection = 50;

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
                        UserAgent = expired ? null : _currentUserAgent,
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
                    context.SetClientUserAgent(_currentUserAgent);
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
                    // The sticky session has expired: the client User-Agent is dropped along
                    // with the rest of the sticky session state.
                    _currentUserAgent = null;
                    _currentUserAgentExplicit = false;
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

        /// <summary>
        /// Clears the sticky session client User-Agent (session reset or sticky session expiry).
        /// </summary>
        public void ClearCurrentUserAgent()
        {
            lock (_lock)
            {
                _currentUserAgent = null;
                _currentUserAgentExplicit = false;
            }
        }

        /// <summary>
        /// Sets the sticky session client User-Agent. An explicit value (set via the Session
        /// API) survives session restarts and is only replaced by another explicit value or
        /// cleared (session reset / sticky session expiry). A value taken passively from
        /// HTTP request headers (explicitSet=false) never overwrites an explicit value.
        /// </summary>
        public void SetCurrentUserAgent(string? userAgent, bool explicitSet)
        {
            if (userAgent == null) return;            
            lock (_lock)
            {
                if (explicitSet || !_currentUserAgentExplicit)
                {
                    _currentUserAgent = userAgent;
                    _currentUserAgentExplicit = explicitSet;
                }
            }
        }

        /// <summary>
        /// Returns the sticky session client User-Agent and whether it was set explicitly
        /// via the Session API (as opposed to being taken passively from HTTP request headers).
        /// </summary>
        public (string? UserAgent, bool Explicit) GetCurrentUserAgent()
        {
            lock (_lock)
            {
                return (_currentUserAgent, _currentUserAgentExplicit);
            }
        }

        /// <summary>
        /// Passively updates the client User-Agent from a parsed HTTP request (h1 or h2).
        /// Never overwrites an explicit value; also records the value on the current session.
        /// </summary>
        public void UpdateClientUserAgent(SessionContext context, HttpRequestHeader requestHeader)
        {
            var userAgent = requestHeader.UserAgent;
            if (string.IsNullOrEmpty(userAgent))
            {
                return;
            }
            context.SetClientUserAgent(userAgent);
            SetCurrentUserAgent(userAgent, explicitSet: false);
        }

        private void RebuildIndex() =>
            _bySession = _byId.Values
                .GroupBy(s => s.SessionId)
                .ToDictionary(g => g.Key, g => g.ToArray());

        public string? AddUrlSubscription(WsConnection conn, string sessionId, string urlPattern)
        {
            var regex = new Regex(urlPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            var id = Guid.NewGuid().ToString("N");

            lock (_subLock)
            {
                if (_byId.Values.Count(s => s.Connection == conn) >= MaxSubscriptionsPerConnection)
                {
                    return null;
                }
                _byId[id] = new Subscription(id, conn, regex, sessionId);
                RebuildIndex();
            }
            return id;
        }

        public bool RemoveUrlSubscription(WsConnection conn, string subscriptionId)
        {
            lock (_subLock)
            {
                if (!_byId.TryGetValue(subscriptionId, out var sub) || sub.Connection != conn)
                {
                    return false;
                }
                _byId.Remove(subscriptionId);
                RebuildIndex();
                return true;
            }
        }

        public void RemoveConnectionSubscriptions(WsConnection conn)
        {
            lock (_subLock)
            {
                var ids = _byId.Values.Where(s => s.Connection == conn).Select(s => s.Id).ToList();
                if (ids.Count == 0) return;
                foreach (var id in ids) _byId.Remove(id);
                RebuildIndex();
            }
        }

        public void NotifyIfRequestUrlMatches(SessionContext context, HttpRequestHeader requestHeader, bool isSecure)
        {
            if (!context.SessionAPI.Enabled || !context.SessionAPI.WebSocketsEnabled)
            {
                return;
            }

            var sessionId = !string.IsNullOrEmpty(context.SessionId) 
                ? context.SessionId 
                : $":{context.InboundEndpoint.Port}"; 
            var method = requestHeader.HTTPVerb;

            if (string.IsNullOrEmpty(method))
            {
                return;
            }

            if (!_bySession.ContainsKey(sessionId))
            {
                return;
            }

            string? interceptedUrl;
            try
            {
                interceptedUrl  = GetHttpRequestUrl(context, requestHeader, isSecure);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Failed to build request URL for session {SessionId}", sessionId);
                return;
            }
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
            byte[]? bytes = null;
            if (!_bySession.TryGetValue(sessionId, out var subs))
            {
                return;
            }
            foreach (var sub in subs)
            {
                var conn = sub.Connection;

                if (!conn.IsOpen()) continue;

                try
                {
                    if (!sub.Filter.IsMatch(url)) continue;
                }
                catch (RegexMatchTimeoutException)
                {
                    _logger.LogWarning("Regex timeout for subscription {SubId}", sub.Id);
                    continue;
                }

                bytes ??= SerializeEvent(sub.Id, sessionId, method, url);

                try
                {
                    await conn.SendTextAsync(bytes, stoppingToken);
                }
                catch
                {
                    RemoveConnectionSubscriptions(sub.Connection); // socket is dead, remove all its subscriptions
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
            int defaultPort = isSecure ? 443 : 80;
            string pathAndQuery = string.IsNullOrEmpty(requestHeader.HTTPTargetPath) ? "/" : requestHeader.HTTPTargetPath;

            if (!string.IsNullOrEmpty(requestHeader.Host))
            {
                // Host header already looks like "host" or "host:port" (or "[::1]:port"), use it as is,
                // dropping only the default port
                string hostAndPort = requestHeader.Host;
                string defaultSuffix = $":{defaultPort}";
                if (hostAndPort.EndsWith(defaultSuffix, StringComparison.Ordinal))
                {
                    hostAndPort = hostAndPort.Substring(0, hostAndPort.Length - defaultSuffix.Length);
                }
                return $"{scheme}://{hostAndPort}{pathAndQuery}";
            }

            var host = requestHeader.HTTPTargetHost ?? context.Host;
            if (host == null)
            {
                return requestHeader.HTTPTargetPath;
            }

            string hostname = host.Hostname.Contains(':') && !host.Hostname.StartsWith('[')
                ? $"[{host.Hostname}]" // IPv6 literal
                : host.Hostname;
            string port = host.Port == defaultPort ? string.Empty : $":{host.Port}";
            return $"{scheme}://{hostname}{port}{pathAndQuery}";
        }

        private static readonly JsonSerializerOptions EventJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        private static byte[] SerializeEvent(string subId, string sessionId, string method, string url)
        {
            var payload = JsonSerializer.Serialize(new
            {
                event_type = "url_matched",
                timestamp = DateTime.UtcNow,
                sub_id = subId,
                session_id = sessionId,
                method,
                url
            }, EventJsonOptions);

            return Encoding.UTF8.GetBytes(payload);
        }
    }
}
