using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Resolvers
{
    public interface IUsernameParameterResolver
    {
        string GenerateSessionId(SessionContext context, string pattern);
        string? ResolveParameterValue(UsernameParameter? parameter, SessionContext context);
    }
}
