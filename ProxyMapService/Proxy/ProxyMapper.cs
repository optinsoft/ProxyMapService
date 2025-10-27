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
            SessionsCounter? sessionsCounter, BytesReadCounter? readCounter, BytesSentCounter? sentCounter,
            ILogger logger, CancellationToken stoppingToken)
        {
            var localEndPoint = new IPEndPoint(IPAddress.Loopback, mapping.Listen.Port);

            async void clientHandler(TcpClient client, CancellationToken token) => 
                await Session.Run(client, mapping, hostRules, userAgent, 
                    sessionsCounter, readCounter, sentCounter, logger, token);

            _listener = new Listener(localEndPoint, clientHandler, logger);

            await _listener.Start(stoppingToken);
        }
    }
}
