using Fare;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Resolvers
{
    public class UsernameParameterResolver : IUsernameParameterResolver
    {
        private string? _currentSessionId = null;
        private int? _currentSessionTime = null;
        private DateTime? _currentSessionExpiresAt = null;
        private UsernameParameterList? _currentSessionUsernameParameters = null;
        private readonly object _lock = new();

        private static readonly string _defaultSessionIdPattern = "^[A-Za-z]{8}";

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
            if (!String.IsNullOrEmpty(username) && parameterList != null)
            {
                foreach (var p in parameterList)
                {
                    string? value = ResolveParameterValue(context, p, DateTime.Now);
                    if (!String.IsNullOrEmpty(value))
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

        public bool IsCurrentSessionExpired(DateTime now)
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
                if (!String.IsNullOrEmpty(_currentSessionId))
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
    }
}
