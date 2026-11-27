using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Ws;
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
        void ClearCurrentUserAgent();
        (string? UserAgent, bool Explicit) GetCurrentUserAgent();
        void SetCurrentUserAgent(string? userAgent, bool explicitSet);
        void UpdateClientUserAgent(SessionContext context, HttpRequestHeader requestHeader);
        string? AddUrlSubscription(WsConnection conn, string sessionId, string urlPattern);
        bool RemoveUrlSubscription(WsConnection conn, string subscriptionId);
        void RemoveConnectionSubscriptions(WsConnection conn);
        void NotifyIfRequestUrlMatches(SessionContext context, HttpRequestHeader requestHeader, bool isSecure);
        Task StartEventProcessingLoop(CancellationToken stoppingToken);
    }
}
