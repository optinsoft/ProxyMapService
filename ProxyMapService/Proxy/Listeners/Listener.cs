using ProxyMapService.Proxy.Sessions;
using System.Net.Sockets;

namespace ProxyMapService.Proxy.Listeners
{
    public class Listener(System.Net.IPEndPoint inboundEndPoint, Action<TcpClient, CancellationToken> incomingClientHandler, ILogger serviceLogger, bool dualMode = false) : IDisposable
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

        public async Task Start(int maxListenerStartRetries, ISessionManager sessionManager, CancellationToken stoppingToken)
        {
            var dualModeActive = dualMode && inboundEndPoint.Address.AddressFamily == AddressFamily.InterNetworkV6;
            // Shown in log messages, e.g. "[::]:5010 (dual mode)".
            var endpointDisplay = dualModeActive ? $"{inboundEndPoint} (dual mode)" : inboundEndPoint.ToString();

            _listener = new TcpListener(inboundEndPoint);
            if (dualModeActive)
            {
                // Enable dual-mode before binding so an IPv6Any listener accepts IPv4 connections too.
                _listener.Server.DualMode = true;
            }

            bool retry = false;
            int retryNum = 0;
            do
            {
                if (retry)
                {
                    retry = false;
                    await Task.Delay(1000, stoppingToken);
                    serviceLogger.LogInformation("Starting listener on {} (retry #{}) ...", endpointDisplay, retryNum);
                }
                else
                {
                    serviceLogger.LogInformation("Starting listener on {} ...", endpointDisplay);
                }
                try
                {
                    _listener.Start();
                }
                catch (SocketException ex)
                {
                    if (ex.ErrorCode == 10048 && retryNum++ < maxListenerStartRetries)
                    {
                        serviceLogger.LogWarning("Unable to start listener on {}\r\n{}",
                            endpointDisplay,
                            "Only one usage of each socket address (protocol/network address/port) is normally permitted");
                        retry = true;
                    }
                    else
                    {
                        serviceLogger.LogError(ex, "Unable to start listener on {}", endpointDisplay);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    serviceLogger.LogError(ex, "Unable to start listener on {}", endpointDisplay);
                    return;
                }
            } while (retry);

            serviceLogger.LogInformation("Listening on {} ...", endpointDisplay);

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
                        _ = Task.Run(
                             async () => await sessionManager.StartEventProcessingLoop(stoppingToken),
                             stoppingToken
                        );
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
                serviceLogger.LogError(ex, "Unexpected Error");
            }

            serviceLogger.LogInformation("Listening on {} has finished", endpointDisplay);
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
