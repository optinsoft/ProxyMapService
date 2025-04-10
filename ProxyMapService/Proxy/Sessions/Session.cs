using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using System.Net;
using System.Net.Sockets;

namespace ProxyMapService.Proxy.Sessions
{
    public class Session
    {
        private static readonly Dictionary<HandleStep, IHandler> Handlers = new()
        {
            //{ HandleStep.Initialize, ForwardHandler.Instance() },
            { HandleStep.Initialize, InitializeHandler.Instance() },
            { HandleStep.Initialized, AuthenticationHandler.Instance() },
            { HandleStep.AuthenticationNotRequired, HostActionHandler.Instance() },
            { HandleStep.Authenticated, HostActionHandler.Instance() },
            { HandleStep.Proxy, ProxyHandler.Instance() },
            { HandleStep.Bypass, BypassHandler.Instance() },
            { HandleStep.Tunnel, TunnelHandler.Instance() }
        };

        public static async Task Run(TcpClient client, ProxyMapping mapping, List<HostRule>? hostRules,
            SessionsCounter? sessionsCounter, BytesReadCounter? readCounter, BytesSentCounter? sentCounter,
            ILogger logger, CancellationToken token)
        {
            using var context = new SessionContext(client, mapping, hostRules, 
                sessionsCounter, readCounter, sentCounter, logger, token);
            sessionsCounter?.OnSessionStarted(context);
            var step = HandleStep.Initialize;
            do
            {
                try
                {
                    step = await Handlers[step].Run(context);
                }
                catch (Exception ex)
                {
                    logger.LogError("Error: {ErrorMessage}", ex.Message);
                    step = HandleStep.Terminate;
                }
            } while (step != HandleStep.Terminate);
        }
    }
}
