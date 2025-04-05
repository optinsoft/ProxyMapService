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
            { HandleStep.AuthenticationNotRequired, ProxyHandler.Instance() },
            { HandleStep.Authenticated, ProxyHandler.Instance() }
        };

        public static async Task Run(TcpClient client, ProxyMapping mapping, 
            SessionsCounter? sessionsCounter, BytesReadCounter? readCounter, BytesSentCounter? sentCounter,
            ILogger logger, CancellationToken token)
        {
            sessionsCounter?.OnSessionStarted();
            var step = HandleStep.Initialize;
            using var context = new SessionContext(client, mapping, sessionsCounter, readCounter, sentCounter, logger, token);
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
