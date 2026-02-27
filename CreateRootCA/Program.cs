using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        string caName = "DO_NOT_TRUST_ProxyMapRoot";

        string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string targetFolder = Path.Combine(userFolder, ".proxymap");

        if (args.Length < 1 && !Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
            Console.WriteLine($"Created directory: {targetFolder}");
        }

        string pfxFilePath = args.Length > 0 ? args[0] : Path.Combine(targetFolder, "ProxyMapService-ca.p12");
        //string pemFilePath = Path.ChangeExtension(pfxFilePath, ".pem");
        string cerCertFilePath = GetCertFilePath(pfxFilePath, ".cer");
        string pemCertFilePath = Path.ChangeExtension(cerCertFilePath, ".pem");

        Console.Write($"Enter password to protect the PFX for '{caName}': ");
        string password = GetPassword();
        Console.WriteLine();

        try
        {
            CreateRootCA(caName, pfxFilePath, //pemFilePath, 
                cerCertFilePath, pemCertFilePath, password);
            Console.WriteLine("\nSuccess!");
            Console.WriteLine($"The certificate with the private key in PKCS12 format: {pfxFilePath}");
            //Console.WriteLine($"The certificate and the private key in PEM format: {pemFilePath}");
            Console.WriteLine($"The public certificate: {cerCertFilePath}");
            Console.WriteLine($"The public certificate in PEM format: {pemCertFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    static void CreateRootCA(string caName, string pfxFilePath, //string pemFilePath, 
        string cerCertFilePath, string pemCertFilePath, string password)
    {
        using (RSA rsa = RSA.Create(4096))
        {
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

            using (X509Certificate2 cert = request.CreateSelfSigned(start, end))
            {
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
        }
    }

    static string ExportPem(X509Certificate2 cert)
    {
        StringBuilder sb = new StringBuilder();

        // Export Public Certificate
        sb.AppendLine("-----BEGIN CERTIFICATE-----");
        sb.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END CERTIFICATE-----");

        return sb.ToString();
    }

    static string ExportFullPem(X509Certificate2 cert, RSA rsa)
    {
        StringBuilder sb = new StringBuilder();

        // Export Public Certificate
        sb.AppendLine("-----BEGIN CERTIFICATE-----");
        sb.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END CERTIFICATE-----");

        // Export Private Key (PKCS#8 format)
        // Note: This exports the key WITHOUT a password in the PEM file.
        sb.AppendLine("-----BEGIN PRIVATE KEY-----");
        sb.AppendLine(Convert.ToBase64String(rsa.ExportPkcs8PrivateKey(), Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END PRIVATE KEY-----");

        return sb.ToString();
    }

    // Helper to mask password input with '*'
    static string GetPassword()
    {
        string pwd = "";
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace && pwd.Length > 0) pwd = pwd[..^1];
            else if (!char.IsControl(key.KeyChar)) pwd += key.KeyChar;
        }
        return pwd;
    }

    static string GetCertFilePath(string path)
    {
        return GetCertFilePath(path, Path.GetExtension(path));
    }

    static string GetCertFilePath(string path, string newExt)
    {
        string ext = Path.GetExtension(path);
        string certFilePath = String.Format("{0}{1}{2}",
            path.Remove(path.Length - ext.Length),
            "-cert",
            newExt);
        return certFilePath;
    }
}

