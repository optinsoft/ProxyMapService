using ProxyMapService.Proxy.Configurations;

namespace ProxyMapService.Proxy.Sessions
{
    public interface ISessionManager
    {
        string? CurrentSessionId { get; }
        int? CurrentSessionTime { get; }
        DateTime? CurrentSessionExpiresAt {  get; }
        SessionInfo CurrentSessionInfo { get; }
        string? GetUsernameWithParameters(SessionContext context, string? username, UsernameParameterList? parameterList);
        void PopulateContext(SessionContext context);
        void ResetSessionId();
    }
}
