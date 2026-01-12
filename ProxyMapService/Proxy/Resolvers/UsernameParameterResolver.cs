using Fare;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Resolvers
{
    public class UsernameParameterResolver : IUsernameParameterResolver
    {
        public string? ResolveParameterValue(UsernameParameter parameter, SessionContext context)
        {
            string? value = parameter.Value;
            string? contextParamValue = null;
            if (value.StartsWith('$'))
            {
                var contextParamName = value.Substring(1);
                contextParamValue = context.UsernameParameters?.GetValue(contextParamName);
                value = contextParamValue ?? parameter.Default;
            }
            if (contextParamValue == null)
            {
                if (value != null && value.StartsWith('^'))
                {
                    var pattern = value.Substring(1);
                    var xeger = new Xeger(pattern);
                    value = xeger.Generate();
                }
            }
            return value;
        }
    }
}
