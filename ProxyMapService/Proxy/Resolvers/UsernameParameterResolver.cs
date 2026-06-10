using Fare;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Resolvers
{
    public class UsernameParameterResolver : IUsernameParameterResolver
    {
        private string _currentSessionId = string.Empty;
        private DateTime? _currentSessionExpiresAt = null;
        private readonly object _lock = new();

        public string CurrentSessionId
        {
            get
            {
                string id;
                lock (_lock)
                {
                    id = _currentSessionId;
                }
                return id;
            }
        }

        public DateTime? CurrentSessionExpiresAt
        {
            get
            {
                DateTime? expiresAt;
                lock (_lock)
                {
                    expiresAt = _currentSessionExpiresAt;
                }
                return expiresAt;
            }
        }

        public bool CurrentSessionExpired
        {
            get
            {
                lock (_lock)
                {
                    return (_currentSessionExpiresAt != null && DateTime.Now >= _currentSessionExpiresAt);
                }
            }
        }

        public string? GetUsernameWithParameters(SessionContext context, string? username, UsernameParameterList? parameterList)
        {
            if (username == null) return null;
            if (parameterList != null)
            {
                foreach (var p in parameterList)
                {
                    string? value = ResolveParameterValue(context, p);
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
            context.SessionTime = context.Mapping.Listen.StickyProxyLifetime;
            if (context.Mapping.Authentication.SetAuthentication)
            {
                ResolveAuthenticationUserParameters(context);
            }
            else if (context.Mapping.Listen.StickyProxyLifetime > 0)
            {
                ResolveSessionTime(context);
                ResolveSessionId(context);
            }
            if (context.SessionId == null && context.SessionTime > 0)
            {
                context.SessionId = GenerateSessionId(context, "^[A-Za-z]{8}");
            }
        }

        public void ResetSessionId()
        {
            lock (_lock)
            {
                _currentSessionId = string.Empty;
                _currentSessionExpiresAt = null;
            }
        }

        private string GenerateSessionId(SessionContext context, string pattern)
        {
            var newId = GenerateValue(pattern);
            lock (_lock)
            {
                if (_currentSessionExpiresAt != null && DateTime.Now < _currentSessionExpiresAt)
                {
                    return _currentSessionId;
                }
                _currentSessionId = newId;
                _currentSessionExpiresAt = DateTime.Now.AddMinutes(context.SessionTime); // context.SessionTime must be set (resolved) before generating session
            }
            return newId;
        }

        private string? ResolveParameterValue(SessionContext context, UsernameParameter? parameter)
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
                value = contextParamValue ?? parameter.Default;
            }
            if (contextParamValue == null)
            {
                if (value != null && value.StartsWith('^'))
                {
                    var pattern = value.Substring(1);
                    if (parameter.SessionId)
                    {
                        value = context.SessionId ?? GenerateSessionId(context, pattern);
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

        private void ResolveSessionId(SessionContext context)
        {
            ResolveParameterValue(context, context.Mapping.Authentication.UsernameParameters.SessionId);
        }

        private void ResolveSessionTime(SessionContext context)
        {
            ResolveParameterValue(context, context.Mapping.Authentication.UsernameParameters.SessionTime);
        }

        private void ResolveAuthenticationUserParameters(SessionContext context)
        {
            // Resolve SessionTime first (before SessionId)
            ResolveSessionTime(context);
            foreach (var p in context.Mapping.Authentication.UsernameParameters)
            {
                if (!p.SessionTime) // Skip already resolved SessionTime
                {
                    ResolveParameterValue(context, p);
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
