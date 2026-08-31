using ProxyMapService.Proxy.Configurations;
using System.Net.WebSockets;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;

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
        void AddOrUpdateUrlSubscription(string sessionId, WebSocket socket, string urlPattern);
        void RemoveUrlSubscription(string sessionId);
        void NotifyIfRequestUrlMatches(SessionContext context, HttpRequestHeader requestHeader, bool isSecure);
        Task StartEventProcessingLoop(CancellationToken stoppingToken);
    }
}
