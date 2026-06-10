using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Resolvers
{
    public interface IUsernameParameterResolver
    {
        string CurrentSessionId { get; }
        DateTime? CurrentSessionExpiresAt {  get; }
        bool CurrentSessionExpired { get; }
        string? GetUsernameWithParameters(SessionContext context, string? username, UsernameParameterList? parameterList);
        void PopulateContext(SessionContext context);
        void ResetSessionId();
    }
}
