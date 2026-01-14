using Fare;
using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Resolvers
{
    public class UsernameParameterResolver : IUsernameParameterResolver
    {
        private string _currentSessionId = string.Empty;
        private DateTime? _currentSessionExpiresAt = null;
        private readonly object _lock = new();

        private static string GenerateValue(string pattern)
        {
            var xeger = new Xeger(pattern);
            return xeger.Generate();
        }

        public string GenerateSessionId(SessionContext context, string pattern)
        {
            var newSessionId = GenerateValue(pattern);
            lock (_lock)
            {
                if (_currentSessionExpiresAt != null && DateTime.Now < _currentSessionExpiresAt)
                {
                    return _currentSessionId;
                }
                _currentSessionId = newSessionId;
                _currentSessionExpiresAt = DateTime.Now.AddMinutes(context.SessionTime); // context.SessionTime must be set (resolved) before generating session
            }
            return newSessionId;
        }

        public string? ResolveParameterValue(UsernameParameter? parameter, SessionContext context)
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
    }
}
