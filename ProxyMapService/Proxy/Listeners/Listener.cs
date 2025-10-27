using System.Net;
using System.Net.Sockets;

namespace ProxyMapService.Proxy.Listeners
{
    public class Listener(IPEndPoint localEndPoint, Action<TcpClient, CancellationToken> clientHandler, ILogger logger) : IDisposable
    {
        private readonly IPEndPoint _localEndPoint = localEndPoint;
        private readonly Action<TcpClient, CancellationToken> _clientHandler = clientHandler;
        private readonly ILogger _logger = logger;
        private TcpListener? _listener;
        private CancellationTokenSource? _cancelSource;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;
            if (_cancelSource != null)
            {
                _cancelSource.Cancel();
                _cancelSource.Dispose();
                _cancelSource = null;
            }
            if (_listener != null)
            {
                _listener.Stop();
                _listener = null;
            }
        }

        public async Task Start(CancellationToken stoppingToken)
        {
            _listener = new TcpListener(_localEndPoint);

            _listener.Start();

            _logger.LogInformation("Listening on {localEndPoint} ...", _localEndPoint);

            await AcceptClients(_listener, stoppingToken);

            _logger.LogInformation("Listening on {localEndPoint} has finished", _localEndPoint);
        }

        protected virtual async Task AcceptClients(TcpListener listener, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                _clientHandler(client, stoppingToken);
            }
        }
    }
}
