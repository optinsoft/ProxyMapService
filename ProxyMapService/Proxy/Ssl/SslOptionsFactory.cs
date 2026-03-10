using ProxyMapService.Proxy.Sessions;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Ssl
{
    public class SslOptionsFactory
    {
        private static SslProtocols ParseProtocols(string protocols)
        {
            SslProtocols result = SslProtocols.None;

            foreach (var protocol in protocols.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                result |= Enum.Parse<SslProtocols>(protocol.Trim(), ignoreCase: true);
            }

            return result;
        }

        public static SslClientAuthenticationOptions BuildSslClientOptions(
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

        public static SslServerAuthenticationOptions BuildSslServerOptions(
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

        public static X509Certificate2 CreateSignedCertificate(string subjectName, string hostname, X509Certificate2 issuerCert)
        {
            using var rsa = RSA.Create(2048);

            var request = new CertificateRequest(
                subjectName,
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add Subject Alternative Name (Crucial for SslStream/Browsers)
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(hostname);
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
