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

        [LoggerMessage(
            EventId = 1315,
            Level = LogLevel.Information,
            Message = "Client {Client} is switched to HTTP/1.1 for {Host}: Host override or cache rules need to inspect and modify requests")]
        private static partial void LogIncomingDowngradedToHttp1(ILogger logger, System.Net.EndPoint? client, HostAddress host);

        [LoggerMessage(
            EventId = 1316,
            Level = LogLevel.Warning,
            Message = "Cannot switch client {Client} to HTTP/1.1 for {Host}: {Reason}. Host override and caching are not applied to this session")]
        private static partial void LogIncomingDowngradeImpossible(ILogger logger, System.Net.EndPoint? client, HostAddress host, string reason);

        #endregion

        /// <summary>
        /// Host override and cache only work with HTTP/1.1 (headers are rewritten, responses are served from a file),
        /// while HTTP/2 is forwarded as is. Therefore, such a session gets HTTP/1.1 from the client's perspective,
        /// and toward the server, if it chose h2, there is a translation from h1 -> h2.
        /// Returns false if switching is not possible (then everything remains as before).
        /// <paramref name="upstreamOptions"/> == null — the server has no TLS, there is nothing to negotiate.
        /// </summary>
        private static bool TryDowngradeIncomingToHttp1(SessionContext context, SslClientAuthenticationOptions? upstreamOptions)
        {
            var clientAlpn = context.ClientHello?.Alpn;
            bool clientHasHttp1 = clientAlpn is not { Count: > 0 } || clientAlpn.Contains(SslApplicationProtocol.Http11);
            if (!clientHasHttp1)
            {
                LogIncomingDowngradeImpossible(context.Logger, context.IncomingEndPoint, context.Host, "the client offers only h2");
                return false;
            }

            if (upstreamOptions?.ApplicationProtocols is { Count: > 0 } configured)
            {
                bool upstreamHttp1 = configured.Contains(SslApplicationProtocol.Http11);
                bool upstreamHttp2 = configured.Contains(SslApplicationProtocol.Http2) && context.H1ToH2TranslationEnabled;
                if (!upstreamHttp1 && !upstreamHttp2)
                {
                    LogIncomingDowngradeImpossible(context.Logger, context.IncomingEndPoint, context.Host,
                        "SslClientOptions allow only h2 and HTTP/1.1 -> HTTP/2 translation is disabled");
                    return false;
                }

                // we offer h2 to the server only if translation is available: the client will get http/1.1 anyway
                var offer = new List<SslApplicationProtocol>(2);
                if (upstreamHttp2) offer.Add(SslApplicationProtocol.Http2);
                if (upstreamHttp1) offer.Add(SslApplicationProtocol.Http11);
                upstreamOptions.ApplicationProtocols = offer;
            }

            LogIncomingDowngradedToHttp1(context.Logger, context.IncomingEndPoint, context.Host);
            return true;
        }

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
                    if (context.DowngradeIncomingToHttp1)
                    {
                        sslServerOptions.ApplicationProtocols = new() { SslApplicationProtocol.Http11 };
                    }
                    else if (sslServerOptions.ApplicationProtocols?.Count > 1)
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
                        context.OutgoingHttp2Client = new Http2ClientConnection(outgoingStream);
                        await context.OutgoingHttp2Client.StartAsync(context.TunnelToken);
                    }
                    else if (context.IncomingHttp2 && context.OutgoingHttp2)
                    {
                        // h2 <-> h2: traffic passes through as-is, but is parsed by a passive observer (headers and bodies logged)
                        context.Http2Observer = new Http2TrafficObserver(
                            new Http2LoggingSink(context),
                            new HpackDecoderAdapter(Http2TrafficObserver.PassiveMaxDynamicTableSize),
                            new HpackDecoderAdapter(Http2TrafficObserver.PassiveMaxDynamicTableSize));
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

            using HttpTunnel requestTunnel = context.Http2Observer != null
                ? new Http2RequestTunnel(
                    context,
                    incomingStream,
                    outgoingStream,
                    outgoingReady,
                    context.ProxyCounters.IncomingReadCounter,
                    context.ProxyCounters.OutgoingSendCounter,
                    context.RequestTunnelState,
                    context.ResponseTunnelState,
                    context.Http2Observer)
                : context.OutgoingHttp2Client != null
                    ? new H1ToH2RequestTunnel(
                        context,
                        incomingStream,
                        outgoingStream,
                        outgoingReady,
                        context.ProxyCounters.IncomingReadCounter,
                        context.ProxyCounters.OutgoingSendCounter,
                        context.RequestTunnelState,
                        context.ResponseTunnelState,
                        context.OutgoingHttp2Client)
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
                    bool downgrade = context.DecryptSSL
                        && context.RequestTunnelState.IsSecure
                        && context.RequiresHttp1Incoming
                        && TryDowngradeIncomingToHttp1(context, sslClientOptions);
                    context.DowngradeIncomingToHttp1 = downgrade;
                    if (!downgrade && sslClientOptions.ApplicationProtocols?.Count > 1)
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
                else if (context.DecryptSSL && context.RequestTunnelState.IsSecure && context.RequiresHttp1Incoming)
                {
                    context.DowngradeIncomingToHttp1 = TryDowngradeIncomingToHttp1(context, null);
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
            
            using HttpTunnel responseTunnel = context.Http2Observer != null
                ? new Http2ResponseTunnel(
                    context,
                    outgoingStream,
                    incomingStream,
                    incomingReady,
                    context.ProxyCounters.OutgoingReadCounter,
                    context.ProxyCounters.IncomingSendCounter,
                    context.ResponseTunnelState,
                    context.RequestTunnelState,
                    context.Http2Observer)
                : context.OutgoingHttp2Client != null
                    ? new H2ToH1ResponseTunnel(
                        context,
                        outgoingStream,
                        incomingStream,
                        incomingReady,
                        context.ProxyCounters.OutgoingReadCounter,
                        context.ProxyCounters.IncomingSendCounter,
                        context.ResponseTunnelState,
                        context.RequestTunnelState,
                        context.OutgoingHttp2Client,
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
