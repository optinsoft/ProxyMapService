using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ssl;
using ProxyMapService.Proxy.Tls;
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
                byte[]? tlsRecord;
                try
                {
                    tlsRecord = context.SslMode == SslMode.No
                       ? null
                        : await context.IncomingStream.PeekTlsRecordAsync(TimeSpan.FromMilliseconds(500), context.Token);
                }
                catch (Exception ex) when (ex is IOException or SocketException)
                {
                    LogTunnelWarning(context.Logger, ex.GetType().Name, ex.Message);
                    return HandleStep.Terminate;
                }

                context.ClientHello = tlsRecord != null ? TlsClientHelloParser.ParseClientHello(tlsRecord) : null;

                bool isIncomingSSL = context.SslMode switch
                {
                    SslMode.Yes => true,
                    SslMode.Auto => tlsRecord != null,
                    _ => false
                };

                bool isOutgoingSSL = context.UpstreamSslMode switch
                {
                    SslMode.Yes => true,
                    SslMode.Auto => NetworkSecurityHelper.IsStandardTlsPort(context.Host.Port) ?
                        true : (NetworkSecurityHelper.IsStandardCleartextPort(context.Host.Port) ? false : isIncomingSSL),
                    _ => false
                };

                context.RequestTunnelState.IsSecure = isIncomingSSL;
                context.ResponseTunnelState.IsSecure = isOutgoingSSL;

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

                    await outgoingReady.Task.WaitAsync(context.TunnelToken);

                    var sslServerOptions = SslOptionsFactory.BuildSslServerOptions(context, serverCertificate);
                    if (sslServerOptions.ApplicationProtocols?.Count > 1)
                    {
                        if (context.NegotiatedAlpn.Protocol.Length > 0)
                            sslServerOptions.ApplicationProtocols = new() { context.NegotiatedAlpn };
                        else
                            sslServerOptions.ApplicationProtocols = null;
                    }

                    await incomingSslStream.AuthenticateAsServerAsync(sslServerOptions, context.TunnelToken);
                    context.Logger.LogServerTLSHandshakeSucceeded(context.IncomingEndPoint, incomingSslStream.NegotiatedApplicationProtocol);
                    context.IncomingStream?.TransferHandlersTo(incomingStream);
                    context.IncomingHttp2 = (incomingSslStream.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2);
                    context.TranslateH1ToH2 = context.H1ToH2TranslationEnabled && context.OutgoingHttp2 && !context.IncomingHttp2;
                    if (context.TranslateH1ToH2)
                    {
                        context.Logger.LogHttp1to2Translation(context.IncomingEndPoint, context.Host);
                        context.OutgoingHttp2Connection = new Http2Connection(outgoingStream);
                        await context.OutgoingHttp2Connection.StartAsync(context.TunnelToken);
                    }
                }
                incomingReady.SetResult();
            }
            catch (OperationCanceledException) when (context.TunnelToken.IsCancellationRequested)
            {
                // Normal tunnel shutdown.
                incomingReady.TrySetCanceled();
                return;
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
                incomingReady.TrySetCanceled();
                return;
                //incomingReady.SetException(ex);
                //throw;
            }
            catch (AuthenticationException ex)
            {
                context.Logger.LogServerTLSHandshakeFailed(context.IncomingEndPoint, ex.InnerException?.Message ?? ex.Message);
                incomingReady.TrySetCanceled();
                return;
                //incomingReady.SetException(ex);
                //throw;
            }
            catch (Exception ex)
            {
                LogRequestTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                incomingReady.TrySetCanceled();
                return;
                //incomingReady.SetException(ex);
                //throw;
            }

            using var requestTunnel = context.OutgoingHttp2Connection != null
                ? new H1ToH2RequestTunnel(
                    context, 
                    incomingStream, 
                    outgoingStream, 
                    outgoingReady,
                    context.ProxyCounters.IncomingReadCounter, 
                    context.ProxyCounters.OutgoingSendCounter,
                    context.RequestTunnelState, 
                    context.ResponseTunnelState,
                    context.OutgoingHttp2Connection)
                : new HttpRequestTunnel(
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
                    if (sslClientOptions.ApplicationProtocols?.Count > 1)
                    {
                        var hello = context.ClientHello;
                        if (hello != null)
                        {
                            var common = hello.Alpn is { Count: > 0 }
                                ? sslClientOptions.ApplicationProtocols.Where(hello.Alpn.Contains).ToList()
                                : new List<SslApplicationProtocol>();

                            sslClientOptions.ApplicationProtocols = common.Count > 0 ? common : null;
                        }
                    }
                    await outgoingSslStream.AuthenticateAsClientAsync(sslClientOptions, context.TunnelToken);
                    context.NegotiatedAlpn = outgoingSslStream.NegotiatedApplicationProtocol;
                    context.Logger.LogClientTLSHandshakeSucceeded(context.Host, outgoingSslStream.NegotiatedApplicationProtocol);
                    context.OutgoingStream?.TransferHandlersTo(outgoingStream);
                    context.OutgoingHttp2 = (outgoingSslStream.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2);
                }
                outgoingReady.SetResult();
                await incomingReady.Task.WaitAsync(context.TunnelToken);
            }
            catch (OperationCanceledException) when (context.TunnelToken.IsCancellationRequested)
            {
                // Normal tunnel shutdown.
                outgoingReady.TrySetCanceled();
                return;
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
                outgoingReady.TrySetCanceled();
                return;
                //outgoingReady.SetException(ex);
                //throw;
            }
            catch (AuthenticationException ex)
            {
                context.Logger.LogClientTLSHandshakeFailed(ex.InnerException?.Message ?? ex.Message, context.Host);
                outgoingReady.TrySetCanceled();
                return;
                //incomingReady.SetException(ex);
                //throw;
            }
            catch (Exception ex)
            {
                LogResponseTunnelError(context.Logger, ex.GetType().Name, ex.Message);
                outgoingReady.TrySetCanceled();
                return;
                //outgoingReady.SetException(ex);
                //throw;
            }
            
            using var responseTunnel = context.OutgoingHttp2Connection != null
                ? new  H2ToH1ResponseTunnel(
                    context, 
                    outgoingStream, 
                    incomingStream, 
                    incomingReady,
                    context.ProxyCounters.OutgoingReadCounter, 
                    context.ProxyCounters.IncomingSendCounter,
                    context.ResponseTunnelState, 
                    context.RequestTunnelState,
                    context.OutgoingHttp2Connection,
                    new HpackDecoderAdapter())
                : new HttpResponseTunnel(
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
