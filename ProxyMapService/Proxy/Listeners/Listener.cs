using ProxyMapService.Proxy.Configurations;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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

            try
            {

                var listenerStopped = false;
                try
                {
                    using (stoppingToken.Register(() =>
                    {
                        listenerStopped = true;
                        _listener.Stop();
                    }))
                    {
                        try
                        {
                            await AcceptClients(_listener, stoppingToken);

                        }
                        catch (ObjectDisposedException)
                        {
                            if (!stoppingToken.IsCancellationRequested)
                            {
                                throw;
                            }
                        }
                        catch (SocketException)
                        {
                            if (!stoppingToken.IsCancellationRequested)
                            {
                                throw;
                            }

                        }
                    }
                }
                finally
                {
                    if (!listenerStopped)
                    {
                        _listener.Stop();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected Error");
            }

            _logger.LogInformation("Listening on {localEndPoint} has finished", _localEndPoint);
        }

        protected virtual async Task AcceptClients(TcpListener listener, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await Task.Run(
                    () => listener.AcceptTcpClientAsync(),
                    stoppingToken);
                if (!stoppingToken.IsCancellationRequested)
                {
                    _clientHandler(client, stoppingToken);
                }
            }
        }
    }
}
