using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Resolvers
{
    public interface IUsernameParameterResolver
    {
        string? ResolveParameterValue(UsernameParameter parameter, SessionContext context);
    }
}
