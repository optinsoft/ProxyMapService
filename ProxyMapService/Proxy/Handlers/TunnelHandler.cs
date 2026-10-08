using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ssl;
using ProxyMapService.Proxy.Tunnels;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Handlers
{
    public partial class TunnelHandler : IHandler
    {
        private static readonly TunnelHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && context.OutgoingStream != null && !context.Token.IsCancellationRequested)
            {
                bool isIncomingSSL;
                try
                {
                    isIncomingSSL = context.SslMode switch
                    {
                        SslMode.Yes => true,
                        SslMode.Auto => await context.IncomingStream.IsTLS(context.Token),
                        _ => false
                    };
                }
                catch (Exception ex) when (ex is IOException or SocketException)
                {
                    LogTunnelWarning(context.Logger, ex.GetType().Name, ex.Message);
                    return HandleStep.Terminate;
                }

                bool isOutgoingSSL = context.UpstreamSslMode switch
                {
                    SslMode.Yes => true,
                    SslMode.Auto => NetworkSecurityHelper.IsStandardTlsPort(context.Host.Port) ?
                        true : (NetworkSecurityHelper.IsStandardCleartextPort(context.Host.Port) ? false : isIncomingSSL),
                    _ => false
                };

                context.RequestTunnelState.IsSecure = isIncomingSSL;
                context.RequestTunnelState.IsHttp2 = false;
                context.ResponseTunnelState.IsSecure = isOutgoingSSL;
                context.ResponseTunnelState.IsHttp2 = false;

                using SslStream? incomingSslStream = context.DecryptSSL && isIncomingSSL ? new(context.IncomingStream) : null;
                using SslStream? outgoingSslStream = context.DecryptSSL && isOutgoingSSL ? new(context.OutgoingStream) : null;

                using CountingStream? incomingSslCountingStream =
                    incomingSslStream != null
                    ? new CountingStream(incomingSslStream, context,
                        context.ProxyCounters.IncomingReadSslCounter, context.ProxyCounters.IncomingSendSslCounter,
                        context.IncomingStream.ReadTunnelId, context.IncomingStream.SendTunnelId)
                    : null;
                using CountingStream? outgoingSslCountingStream =
                    outgoingSslStream != null
                    ? new CountingStream(outgoingSslStream, context,
                        context.ProxyCounters.OutgoingReadSslCounter, context.ProxyCounters.OutgoingSendSslCounter,
                        context.OutgoingStream.ReadTunnelId, context.OutgoingStream.SendTunnelId)
                    : null;

                var incomingReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var outgoingReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                var incomingStream = incomingSslCountingStream ?? context.IncomingStream;
                var outgoingStream = outgoingSslCountingStream ?? context.OutgoingStream;

                using var tunnelCts = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
                context.TunnelToken = tunnelCts.Token;

                var requestTunnelTask = RunRequestTunnel(
                    context,
                    incomingSslStream,
                    incomingStream,
                    outgoingStream,
                    incomingReady,
                    outgoingReady);

                var responseTunnelTask = RunResponseTunnel(
                    context,
                    outgoingSslStream,
                    incomingStream,
                    outgoingStream,
                    incomingReady,
                    outgoingReady);

                try
                {
                    await Task.WhenAny(requestTunnelTask, responseTunnelTask);

                    tunnelCts.Cancel();

                    await Task.WhenAll(requestTunnelTask, responseTunnelTask);
                }
                catch (OperationCanceledException) when (context.Token.IsCancellationRequested)
                {
                }
            }

            return HandleStep.Terminate;
        }

        public static TunnelHandler Instance()
        {
            return Self;
        }

        #region High-Performance Logging

        [LoggerMessage(
            EventId = 1301,
            Level = LogLevel.Error,
            Message = "[RequestTunnel] {ExceptionName}: {ErrorMessage}")]
        private static partial void LogRequestTunnelError(ILogger logger, string exceptionName, string errorMessage);

        [LoggerMessage(
            EventId = 1302,
            Level = LogLevel.Error,
            Message = "[ResponseTunnel] {ExceptionName}: {ErrorMessage}")]
        private static partial void LogResponseTunnelError(ILogger logger, string exceptionName, string errorMessage);

        [LoggerMessage(
            EventId = 1311,
            Level = LogLevel.Warning,
            Message = "[RequestTunnel] {ExceptionName}: {ErrorMessage}")]
        private static partial void LogRequestTunnelWarning(ILogger logger, string exceptionName, string errorMessage);

        [LoggerMessage(
            EventId = 1312,
            Level = LogLevel.Warning,
            Message = "[ResponseTunnel] {ExceptionName}: {ErrorMessage}")]
        private static partial void LogResponseTunnelWarning(ILogger logger, string exceptionName, string errorMessage);

        [LoggerMessage(
            EventId = 1313,
            Level = LogLevel.Warning,
            Message = "[Tunnel] {ExceptionName}: {ErrorMessage}")]
        private static partial void LogTunnelWarning(ILogger logger, string exceptionName, string errorMessage);

        #endregion

        private static async Task RunRequestTunnel(SessionContext context,
            SslStream? incomingSslStream, CountingStream incomingStream, CountingStream outgoingStream,
            TaskCompletionSource incomingReady, TaskCompletionSource outgoingReady)
        {
            try
            {
                if (incomingSslStream != null)
                {
                    string subjectName = $"CN=*.{context.Host.OriginalHostname}";
                    X509Certificate2? serverCertificate = context.ServerCertificate;
                    using X509Certificate2? tempCertificate = serverCertificate == null && context.CACertificate != null
                        ? SslOptionsFactory.CreateSignedCertificate(subjectName, context.Host.OriginalHostname, context.CACertificate)
                        : null;
                    serverCertificate ??= tempCertificate ?? throw new NullServerCertificateException();
                    var sslServerOptions = SslOptionsFactory.BuildSslServerOptions(context, serverCertificate);
                    await incomingSslStream.AuthenticateAsServerAsync(sslServerOptions, context.TunnelToken);
                    context.Logger.LogServerTLSHandshakeSucceeded();
                    context.IncomingStream?.TransferHandlersTo(incomingStream);
                }
                incomingReady.SetResult();
            }
            catch (OperationCanceledException) when (context.TunnelToken.IsCancellationRequested)
            {
                // Normal tunnel shutdown.
            }
            catch (ObjectDisposedException ex)
            {
                LogRequestTunnelWarning(context.Logger, ex.GetType().Name, ex.Message);
                //return;
                incomingReady.SetException(ex);
                throw;
            }
            catch (IOException ex)
            {
                LogRequestTunnelWarning(context.Logger, ex.GetType().Name, ex.Message);
                //return;
                incomingReady.SetException(ex);
                throw;
            }
            catch (AuthenticationException ex)
            {
                context.Logger.LogServerTLSHandshakeFailed(ex.InnerException?.Message ?? ex.Message);
                return;
                //incomingReady.SetException(ex);
                //throw;
            }
            catch (Exception ex)
            {
                LogRequestTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                return;
                //incomingReady.SetException(ex);
                //throw;
            }

            var requestTunnel = new HttpRequestTunnel(
                context, 
                incomingStream, 
                outgoingStream, 
                outgoingReady,
                context.ProxyCounters.IncomingReadCounter, 
                context.ProxyCounters.OutgoingSendCounter,
                context.RequestTunnelState, 
                context.ResponseTunnelState);

            await requestTunnel.RunAsync();
        }

        private static async Task RunResponseTunnel(SessionContext context,
            SslStream? outgoingSslStream, CountingStream incomingStream, CountingStream outgoingStream,
            TaskCompletionSource incomingReady, TaskCompletionSource outgoingReady)
        {
            try
            {
                if (outgoingSslStream != null)
                {
                    var sslClientOptions = SslOptionsFactory.BuildSslClientOptions(context);
                    if (context.IgnoreCertificateErrors)
                    {
                        sslClientOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                    }
                    await outgoingSslStream.AuthenticateAsClientAsync(sslClientOptions, context.TunnelToken);
                    context.Logger.LogClientTLSHandshakeSucceeded(context.Host, outgoingSslStream.NegotiatedApplicationProtocol);
                    context.OutgoingStream?.TransferHandlersTo(outgoingStream);
                    context.ResponseTunnelState.IsHttp2 = (outgoingSslStream.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2);
                }
                outgoingReady.SetResult();
            }
            catch (OperationCanceledException) when (context.TunnelToken.IsCancellationRequested)
            {
                // Normal tunnel shutdown.
            }
            catch (ObjectDisposedException ex)
            {
                LogResponseTunnelWarning(context.Logger, ex.GetType().Name, ex.Message);
                //return;
                outgoingReady.SetException(ex);
                throw;
            }
            catch (IOException ex)
            {
                LogResponseTunnelWarning(context.Logger, ex.GetType().Name, ex.Message);
                //return;
                outgoingReady.SetException(ex);
                throw;
            }
            catch (AuthenticationException ex)
            {
                context.Logger.LogClientTLSHandshakeFailed(ex.InnerException?.Message ?? ex.Message, context.Host);
                return;
                //incomingReady.SetException(ex);
                //throw;
            }
            catch (Exception ex)
            {
                LogResponseTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                return;
                //outgoingReady.SetException(ex);
                //throw;
            }
            
            var responseTunnel = new  HttpResponseTunnel(
                context, 
                outgoingStream, 
                incomingStream, 
                incomingReady,
                context.ProxyCounters.OutgoingReadCounter, 
                context.ProxyCounters.IncomingSendCounter,
                context.ResponseTunnelState, 
                context.RequestTunnelState);

            await responseTunnel.RunAsync();
        }
    }
}
