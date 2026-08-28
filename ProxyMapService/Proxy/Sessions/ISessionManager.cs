using ProxyMapService.Proxy.Configurations;
using System.Net.WebSockets;

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
        void AddOrUpdateSubscription(string sessionId, WebSocket socket, string urlPattern);
        void RemoveSubscription(string sessionId);
        void NotifyIfMatches(string interceptedUrl, object requestData);
    }
}
