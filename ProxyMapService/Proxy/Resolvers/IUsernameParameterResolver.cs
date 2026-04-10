using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Resolvers
{
    public interface IUsernameParameterResolver
    {
        string CurrentSessionId { get; }
        DateTime? CurrentSessionExpiresAt {  get; }
        bool CurrentSessionExpired { get; }
        string GenerateSessionId(SessionContext context, string pattern);
        void ResetSessionId();
        string? ResolveParameterValue(SessionContext context, UsernameParameter? parameter);
    }
}
