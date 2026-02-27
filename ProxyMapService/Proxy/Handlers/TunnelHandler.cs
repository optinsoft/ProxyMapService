using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
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
                
                string subjectName = $"CN=*.{context.Host.Hostname}";
                X509Certificate2? serverCertificate = context.ServerCertificate;

                if (outgoingSslStream != null)
                {
                    await outgoingSslStream.AuthenticateAsClientAsync(BuildSslClientOptions(context), context.Token);
                    if (outgoingSslStream.RemoteCertificate != null)
                    {
                        var cert = new X509Certificate2(outgoingSslStream.RemoteCertificate);
                        context.Logger.LogDebug("Server Certificate Subject: {}", cert.Subject);
                        subjectName = cert.Subject;
                    }
                }

                if (incomingSslStream != null)
                {
                    if (serverCertificate == null)
                    {
                        if (context.CACertificate != null)
                        {
                            serverCertificate = CreateSignedCertificate(context, subjectName, context.CACertificate);
                        }
                        else
                        {
                            throw new NullServerCertificateException();
                        }
                    }
                    await incomingSslStream.AuthenticateAsServerAsync(BuildSslServerOptions(context, serverCertificate), context.Token);
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
            var forwardTask = context.Host.Overwritten 
                ? OverrideHostTunnel(incomingStream, outgoingStream, context, context.IncomingReadCounter, context.OutgoingSentCounter)
                : Tunnel(incomingStream, outgoingStream, context, context.IncomingReadCounter, context.OutgoingSentCounter);
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

        private static async Task OverrideHostTunnel(Stream source, Stream destination, SessionContext context,
        IBytesReadCounter? readCounter, IBytesSentCounter? sentCounter)
        {

            var tunnelId = ++_tunnelId;

            var buffer = new byte[BufferSize];
            using var ms = new MemoryStream();

            CancellationToken token = context.Token;

            try
            {
                int bytesRead, headersEnd, searchStart = 0;
                bool readHeaders = true;
                do
                {
                    if (readCounter != null && readCounter.IsLogReading)
                    {
                        context.Logger.LogDebug("Tunnel {tunnelId}: reading from {direction}...", tunnelId, StreamDirectionName.GetName(readCounter.Direction));
                    }
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    if (bytesRead > 0)
                    {
                        if (readHeaders)
                        {
                            ms.Write(buffer, 0, bytesRead);
                            if ((headersEnd = HttpHostRewriter.FindHeadersEnd(ms, ref searchStart)) >= 0 || searchStart < 0)
                            {
                                readHeaders = false;
                                var sendBuffer = HttpHostRewriter.OverrideHostHeader(ms, headersEnd, context.Host.Hostname, context.Host.Port) ?? buffer.AsMemory(0, bytesRead);
                                if (sentCounter != null && sentCounter.IsLogSending)
                                {
                                    context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...", tunnelId, StreamDirectionName.GetName(sentCounter.Direction));
                                }
                                await destination.WriteAsync(sendBuffer, token);
                            }
                        }
                        else
                        {
                            if (sentCounter != null && sentCounter.IsLogSending)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...", tunnelId, StreamDirectionName.GetName(sentCounter.Direction));
                            }
                            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        }
                    }
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException)
            {
                //context.Logger.LogError("ObjectDisposedException: {ErrorMessage}", ex.Message);
            }
        }

        private static SslProtocols ParseProtocols(string protocols)
        {
            SslProtocols result = SslProtocols.None;

            foreach (var protocol in protocols.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                result |= Enum.Parse<SslProtocols>(protocol.Trim(), ignoreCase: true);
            }

            return result;
        }

        private static SslClientAuthenticationOptions BuildSslClientOptions(
            SessionContext context)
        {
            var protocols = ParseProtocols(context.SslClientConfig.EnabledSslProtocols);
            return new SslClientAuthenticationOptions
            {
                TargetHost = context.Host.Hostname,
                EnabledSslProtocols = protocols,
                CertificateRevocationCheckMode = context.SslClientConfig.CheckCertificateRevocation
                    ? X509RevocationMode.Online
                    : X509RevocationMode.NoCheck
            };
        }

        private static SslServerAuthenticationOptions BuildSslServerOptions(
            SessionContext context, X509Certificate2 serverCertificate)
        {
            var protocols = ParseProtocols(context.SslServerConfig.EnabledSslProtocols);
            return new SslServerAuthenticationOptions
            {
                EnabledSslProtocols = protocols,
                ClientCertificateRequired = context.SslServerConfig.ClientCertificateRequired,
                ServerCertificate = serverCertificate,
                CertificateRevocationCheckMode = context.SslServerConfig.CheckCertificateRevocation
                    ? X509RevocationMode.Online
                    : X509RevocationMode.NoCheck
            };
        }

        private static X509Certificate2 CreateSignedCertificate(SessionContext context, string subjectName,  X509Certificate2 issuerCert)
        {
            using var rsa = RSA.Create(2048);
            
            var request = new CertificateRequest(
                subjectName, 
                rsa, 
                HashAlgorithmName.SHA256, 
                RSASignaturePadding.Pkcs1);

            // Add Subject Alternative Name (Crucial for SslStream/Browsers)
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(context.Host.Hostname);
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Standard TLS Server usage
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { 
                        new Oid("1.3.6.1.5.5.7.3.1") 
                    }, 
                    false));

            // Create a unique Serial Number
            byte[] serialNumber = Guid.NewGuid().ToByteArray();

            // SIGN the request using the CA's private key
            using X509Certificate2 ephemeralCert = request.Create(
                issuerCert,
                DateTimeOffset.Now.AddDays(-1),
                DateTimeOffset.Now.AddYears(2),
                serialNumber);

            // Join the private key to the public cert
            var certWithKey = ephemeralCert.CopyWithPrivateKey(rsa);

            // FIX: Export and Re-import to move the key from Ephemeral -> Persisted
            return new X509Certificate2(certWithKey.Export(X509ContentType.Pfx));
        }
    }
}
