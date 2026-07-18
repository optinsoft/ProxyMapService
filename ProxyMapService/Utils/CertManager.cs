using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ProxyMapService.Utils
{
    public class CertManager(ILogger logger)
    {
        public void CreateProxyMapRootCertificate(string caName)
        {
            string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string targetFolder = Path.Combine(userFolder, ".proxymap");

            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
                logger.LogWarning("Created directory: {targetFolder}", targetFolder);
            }

            string pfxFilePath = Path.Combine(targetFolder, "ProxyMapService-ca.p12");
            //string pemFilePath = Path.ChangeExtension(pfxFilePath, ".pem");
            string cerCertFilePath = GetCertFilePath(pfxFilePath, ".cer");
            string pemCertFilePath = Path.ChangeExtension(cerCertFilePath, ".pem");

            if (File.Exists(pfxFilePath))
            {
                //logger.LogWarning("Failed to create root certificate. File already exists: {pfxFilePath}", pfxFilePath);
                return;
            }

            //if (File.Exists(pemFilePath))
            //{
            //    logger.LogWarning("Failed to create root certificate. File already exists: {pemFilePath}", pemFilePath);
            //    return;
            //}

            if (File.Exists(cerCertFilePath))
            {
                //logger.LogWarning("Failed to create root certificate. File already exists: {cerCertFilePath}", cerCertFilePath);
                return;
            }

            if (File.Exists(pemCertFilePath))
            {
                //logger.LogWarning("Failed to create root certificate. File already exists: {pemCertFilePath}", pemCertFilePath);
                return;
            }


            CreateRootCA(caName, pfxFilePath, //pemFilePath, 
                cerCertFilePath, pemCertFilePath, "");

            logger.LogWarning("Root certificate created in: {targetFolder}", targetFolder);
            logger.LogWarning("The certificate with the private key in PKCS12 format: {pfxFilePath}", pfxFilePath);
            //logger.LogWarning($"The certificate and the private key in PEM format: {pemFilePath}", pemFilePath);
            logger.LogWarning("The public certificate: {cerCertFilePath}", cerCertFilePath);
            logger.LogWarning("The public certificate in PEM format: {pemCertFilePath}", pemCertFilePath);
        }

        private static void CreateRootCA(string caName, string pfxFilePath, //string pemFilePath, 
            string cerCertFilePath, string pemCertFilePath, string password)
        {
            using RSA rsa = RSA.Create(4096);

            var request = new CertificateRequest(
                $"CN={caName}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // 1. Mark as a Certificate Authority (CA)
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, false, 0, true));

            // 2. Set Key Usage for signing other certs
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));

            // 3. Set Subject Key Identifier (standard for CAs)
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            // 4. Server Auth
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection {
                        new Oid("1.3.6.1.5.5.7.3.1")
                    },
                    false));

            // 5. Create the self-signed certificate (valid for 10 years)
            DateTimeOffset start = DateTimeOffset.UtcNow.AddDays(-1);
            DateTimeOffset end = start.AddYears(10);

            using X509Certificate2 cert = request.CreateSelfSigned(start, end);

            // 5. Export to PFX with the password
            byte[] pfxData = cert.Export(X509ContentType.Pfx, password);
            File.WriteAllBytes(pfxFilePath, pfxData);

            // 6. Export to PEM with the private key
            //string pemFullText = ExportFullPem(cert, rsa);
            //File.WriteAllText(pemFilePath, pemFullText);

            // 7. Export to public CER
            byte[] publicData = cert.RawData;
            File.WriteAllBytes(cerCertFilePath, publicData);

            // 8. Export to public PEM
            string pemText = ExportPem(cert);
            File.WriteAllText(pemCertFilePath, pemText);
        }

        private static string ExportPem(X509Certificate2 cert)
        {
            StringBuilder sb = new StringBuilder();

            // Export Public Certificate
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            sb.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            sb.AppendLine("-----END CERTIFICATE-----");

            return sb.ToString();
        }

        private static string GetCertFilePath(string path, string newExt)
        {
            string ext = Path.GetExtension(path);
            string certFilePath = String.Format("{0}{1}{2}",
                path.Remove(path.Length - ext.Length),
                "-cert",
                newExt);
            return certFilePath;
        }
    }
}
