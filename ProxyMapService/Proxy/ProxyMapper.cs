using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Listeners;
using ProxyMapService.Proxy.Sessions;
using System.Data;
using System.Net;
using System.Net.Sockets;

namespace ProxyMapService.Proxy
{
    public class ProxyMapper(ProxyMapping mapping, ILogger logger) : IDisposable
    {
        private readonly ProxyMapping _mapping = mapping;
        private readonly ILogger _logger = logger;
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

        public async Task Start()
        {
            var localEndPoint = new IPEndPoint(IPAddress.Loopback, _mapping.Listen.Port);

            async void clientHandler(TcpClient client, CancellationToken token) => await Session.Run(client, _mapping, _logger, token);

            _listener = new Listener(localEndPoint, clientHandler, _logger);

            await _listener.Start();
        }
    }
}
