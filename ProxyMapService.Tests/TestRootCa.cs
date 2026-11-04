using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ProxyMapService.Tests
{
    /// <summary>
    /// Test root CA for the HTTPS test servers.
    /// Created on first use in ~/.proxymap (same folder as the real ProxyMapService CA),
    /// so the proxy can trust it via SslClientOptions:TrustedCAs.
    /// Files:
    ///   ~/.proxymap/ProxyMapService-Test-ca.p12         (CA with private key, password "test")
    ///   ~/.proxymap/ProxyMapService-Test-ca-cert.pem    (public certificate, PEM)
    /// </summary>
    public static class TestRootCa
    {
        public const string Subject = "DO_NOT_TRUST_ProxyMapTestRoot";
        private const string Password = "test";

        private static readonly object _createLock = new();

        public static string Directory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".proxymap");

        public static string PfxPath => Path.Combine(Directory, "ProxyMapService-Test-ca.p12");
        public static string PemPath => Path.Combine(Directory, "ProxyMapService-Test-ca-cert.pem");

        private static readonly Lazy<X509Certificate2> _certificate = new(EnsureCreated);

        /// <summary>CA certificate with the private key (signs the leaf certificates of test servers).</summary>
        public static X509Certificate2 Certificate => _certificate.Value;

        private static X509Certificate2 EnsureCreated()
        {
            if (File.Exists(PfxPath))
                return new X509Certificate2(PfxPath, Password);

            lock (_createLock)
            {
                if (File.Exists(PfxPath))
                    return new X509Certificate2(PfxPath, Password);

                System.IO.Directory.CreateDirectory(Directory);

                using var rsa = RSA.Create(2048);
                var request = new CertificateRequest(
                    $"CN={Subject}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                // Certificate Authority
                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(true, false, 0, true));
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
                request.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

                using var ca = request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddYears(10));

                File.WriteAllBytes(PfxPath, ca.Export(X509ContentType.Pfx, Password));
                File.WriteAllText(PemPath, ExportPem(ca));

                return new X509Certificate2(PfxPath, Password);
            }
        }

        private static string ExportPem(X509Certificate2 cert)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            sb.AppendLine(Convert.ToBase64String(
                cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            sb.AppendLine("-----END CERTIFICATE-----");
            return sb.ToString();
        }
    }
}
