using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Sessions;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Handlers
{
    public class TunnelHandler : IHandler
    {
        private static readonly TunnelHandler Self = new();
        private const int BufferSize = 8192;
        private static int _tunnelId = 0;

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && context.OutgoingStream != null && !context.Token.IsCancellationRequested)
            {
                using SslStream? incomingSslStream = context.Ssl ? new(context.IncomingStream) : null;
                using SslStream? outgoingSslStream = context.UpstreamSsl ? new(context.OutgoingStream) : null;

                if (incomingSslStream != null)
                {
                    if (context.ServerCertificate == null)
                    {
                        throw new NullServerCertificateException();
                    }

                    await incomingSslStream.AuthenticateAsServerAsync(
                        context.ServerCertificate,
                        clientCertificateRequired: false, // Set to true if you require client certificates
                        enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13, // Specify supported protocols
                        checkCertificateRevocation: true);
                }

                if (outgoingSslStream != null)
                {
                    await outgoingSslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = context.HostName, // MUST match certificate CN/SAN
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.Online
                    });
                }

                using CountingStream? incomingSslCountingStream = incomingSslStream != null ? new CountingStream(incomingSslStream, context, context.IncomingSslCounter, null) : null;
                using CountingStream? outgoingSslCountingStream = outgoingSslStream != null ? new CountingStream(outgoingSslStream, context, context.OutgoingSslCounter, null) : null;

                await TwoWayTunnel(context, incomingSslCountingStream ?? context.IncomingStream, outgoingSslCountingStream ?? context.OutgoingStream);
            }

            return HandleStep.Terminate;
        }

        public static TunnelHandler Instance()
        {
            return Self;
        }

        private static async Task TwoWayTunnel(SessionContext context, Stream incomingStream, Stream outgoingStream)
        {
            var forwardTask = Tunnel(incomingStream, outgoingStream, context, context.IncomingReadCounter, context.OutgoingSentCounter);
            var reverseTask = Tunnel(outgoingStream, incomingStream, context, context.OutgoingReadCounter, context.IncomingSentCounter);
            await Task.WhenAny(forwardTask, reverseTask);
        }

        private static async Task Tunnel(Stream source, Stream destination, SessionContext context,
            IBytesReadCounter? readCounter, IBytesSentCounter? sentCounter)
        {
           
            var tunnelId = ++_tunnelId;
            
            var buffer = new byte[BufferSize];

            CancellationToken token = context.Token;

            try
            {
                int bytesRead;
                do
                {
                    if (readCounter != null && readCounter.IsLogReading)
                    {
                        context.Logger.LogDebug("Tunnel {tunnelId}: reading from {direction}...", tunnelId, StreamDirectionName.GetName(readCounter.Direction));
                    }
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    if (bytesRead > 0)
                    {
                        if (sentCounter != null && sentCounter.IsLogSending)
                        {
                            context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...", tunnelId, StreamDirectionName.GetName(sentCounter.Direction));
                        }
                        await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                    }
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException)
            {
                //context.Logger.LogError("ObjectDisposedException: {ErrorMessage}", ex.Message);
            }
        }
    }
}
