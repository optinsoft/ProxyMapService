using System.Net.Sockets;

namespace ProxyMapService.Proxy.Listeners
{
    public class Listener(System.Net.IPEndPoint incomingEndPoint, Action<TcpClient, CancellationToken> incomingClientHandler, ILogger logger) : IDisposable
    {
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

        public async Task Start(int maxListenerStartRetries, CancellationToken stoppingToken)
        {
            _listener = new TcpListener(incomingEndPoint);

            bool retry = false;
            int retryNum = 0;
            do
            {
                if (retry)
                {
                    retry = false;
                    await Task.Delay(1000, stoppingToken);
                    logger.LogInformation("Starting listener on {} (retry #{}) ...", incomingEndPoint, retryNum);
                }
                else
                {
                    logger.LogInformation("Starting listener on {} ...", incomingEndPoint);
                }
                try
                {
                    _listener.Start();
                }
                catch (SocketException ex)
                {
                    if (ex.ErrorCode == 10048 && retryNum++ < maxListenerStartRetries)
                    {
                        logger.LogWarning("Unable to start listener on {}\r\n{}",
                            incomingEndPoint,
                            "Only one usage of each socket address (protocol/network address/port) is normally permitted");
                        retry = true;
                    }
                    else
                    {
                        logger.LogError(ex, "Unable to start listener on {}", incomingEndPoint);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to start listener on {}", incomingEndPoint);
                    return;
                }
            } while (retry);

            logger.LogInformation("Listening on {} ...", incomingEndPoint);

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
                logger.LogError(ex, "Unexpected Error");
            }

            logger.LogInformation("Listening on {} has finished", incomingEndPoint);
        }

        protected virtual async Task AcceptClients(TcpListener listener, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var incomingClient = await Task.Run(
                    () => listener.AcceptTcpClientAsync(),
                    stoppingToken);
                if (!stoppingToken.IsCancellationRequested)
                {
                    incomingClientHandler(incomingClient, stoppingToken);
                }
            }
        }
    }
}
