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

        public string GenerateSessionId(SessionContext context, string pattern)
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

        public void ResetSessionId()
        {
            lock (_lock)
            {
                _currentSessionId = string.Empty;
                _currentSessionExpiresAt = null;
            }
        }

        public string? ResolveParameterValue(SessionContext context, UsernameParameter? parameter)
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

        private static string GenerateValue(string pattern)
        {
            var xeger = new Xeger(pattern);
            return xeger.Generate();
        }
    }
}
