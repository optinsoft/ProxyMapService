using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Listeners;
using ProxyMapService.Proxy.Sessions;
using System.Data;
using System.Net;
using System.Net.Sockets;

namespace ProxyMapService.Proxy
{
    public class ProxyMapper : IDisposable
    {
        private Listener? _listener;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) 
                return;
            if (_listener != null)
            {
                _listener.Dispose();
                _listener = null;
            }
        }

        public async Task Start(ProxyMapping mapping, List<HostRule>? hostRules, string? userAgent,
            ISessionsCounter? sessionsCounter, IBytesReadCounter? remoteReadCounter, IBytesSentCounter? remoteSentCounter,
            IBytesReadCounter? clientReadCounter, IBytesSentCounter? clientSentCounter, 
            ILogger logger, int maxListenerStartRetries, CancellationToken stoppingToken)
        {
            var localEndPoint = new IPEndPoint(IPAddress.Loopback, mapping.Listen.Port);

            var proxyChanger = new ProxyChanger(mapping.ProxyServers);

            async void clientHandler(TcpClient client, CancellationToken token) => 
                await Session.Run(client, mapping, proxyChanger, hostRules, userAgent, 
                    sessionsCounter, remoteReadCounter, remoteSentCounter,
                    clientReadCounter, clientSentCounter,
                    logger, token);

            _listener = new Listener(localEndPoint, clientHandler, logger);

            await _listener.Start(maxListenerStartRetries, stoppingToken);
        }
    }
}
