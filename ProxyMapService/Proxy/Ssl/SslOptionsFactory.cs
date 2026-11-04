using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Ssl
{
    public class SslOptionsFactory
    {
        private static readonly ConcurrentDictionary<SslClientOptionsConfig, IReadOnlyList<X509Certificate2>> _trustedCaCache = new();

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
            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = context.Host.Hostname,
                EnabledSslProtocols = protocols,
                CertificateRevocationCheckMode = context.SslClientConfig.CheckCertificateRevocation
                    ? X509RevocationMode.Online
                    : X509RevocationMode.NoCheck,
                ApplicationProtocols = context.SslClientConfig.ApplicationProtocols
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => new SslApplicationProtocol(p.Trim()))
                    .ToList()
            };

            var trustedCas = GetTrustedCAs(context.SslClientConfig);
            if (trustedCas.Count > 0)
            {
                // Если IgnoreCertificateErrors выставлен, TunnelHandler перекроет этот callback
                // на "всегда true" — он ставится после BuildSslClientOptions.
                sslOptions.RemoteCertificateValidationCallback = (_, leaf, _, status) =>
                    ValidateAgainstTrustedCas(leaf, status, trustedCas);
            }

            return sslOptions;
        }

        /// <summary>
        /// Accepts the upstream certificate if standard validation passes, or if the chain pins to one
        /// of the configured trusted CAs. An untrusted root is the only error we are willing to
        /// forgive in that case; hostname mismatch / expired / revoked are still enforced.
        /// </summary>
        private static bool ValidateAgainstTrustedCas(
            X509Certificate? leaf, SslPolicyErrors errors, IReadOnlyList<X509Certificate2> trustedCas)
        {
            if (leaf == null) return false;

            if (errors == SslPolicyErrors.None)
                return true;

            if (errors != SslPolicyErrors.RemoteCertificateChainErrors)
                return false; // name mismatch, expired, revoked, ...

            const X509ChainStatusFlags allowedUntrustedRootFlags =
                X509ChainStatusFlags.UntrustedRoot
                | X509ChainStatusFlags.PartialChain;

            var leaf2 = new X509Certificate2(leaf);

            foreach (var ca in trustedCas)
            {
                using var chain = new X509Chain();
                chain.ChainPolicy.ExtraStore.Add(ca);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                // Build() returns false when the root is untrusted; we inspect the chain ourselves.
                chain.Build(leaf2);

                if (chain.ChainElements.Count == 0) continue;

                bool pinnedToConfiguredCa = chain.ChainElements[^1].Certificate.Thumbprint
                    .Equals(ca.Thumbprint, StringComparison.OrdinalIgnoreCase);
                bool noOtherErrors = chain.ChainStatus
                    .All(s => (s.Status & ~allowedUntrustedRootFlags) == 0);

                if (pinnedToConfiguredCa && noOtherErrors)
                    return true;
            }

            return false;
        }

        private static IReadOnlyList<X509Certificate2> GetTrustedCAs(SslClientOptionsConfig config)
        {
            if (config.TrustedCAs.Count == 0) return [];

            return _trustedCaCache.GetOrAdd(config, c =>
                c.TrustedCAs
                    .Select(ResolveTrustedCa)
                    .Where(c => c != null)
                    .Select(c => c!)
                    .ToList());
        }

        private static X509Certificate2? ResolveTrustedCa(TrustedCaConfig ca)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ca.PemFile))
                    // CreateFromPem(path) is broken in some .NET builds — read the text first.
                    return X509Certificate2.CreateFromPem(File.ReadAllText(ExpandPath(ca.PemFile)));

                if (!string.IsNullOrWhiteSpace(ca.CerFile))
                    return new X509Certificate2(ExpandPath(ca.CerFile));

                if (!string.IsNullOrWhiteSpace(ca.StoreName) && !string.IsNullOrWhiteSpace(ca.Subject))
                {
                    var location = Enum.TryParse<StoreLocation>(ca.StoreLocation, ignoreCase: true, out var l)
                        ? l : StoreLocation.CurrentUser;
                    using var store = new X509Store(
                        Enum.Parse<StoreName>(ca.StoreName, ignoreCase: true), location);
                    store.Open(OpenFlags.ReadOnly);
                    var found = store.Certificates
                        .Find(X509FindType.FindBySubjectDistinguishedName, ca.Subject, validOnly: true)
                        .Cast<X509Certificate2>()
                        .FirstOrDefault();
                    store.Close();
                    return found;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SslOptionsFactory] Failed to load trusted CA ({ca.PemFile ?? ca.CerFile ?? $"store:{ca.StoreName}:{ca.Subject}"}): {ex.Message}");
                return null;
            }
        }

        private static string ExpandPath(string path)
        {
            if (path.StartsWith("~/", StringComparison.Ordinal))
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);

            return Path.GetFullPath(path);
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
                    : X509RevocationMode.NoCheck,
                ApplicationProtocols = context.SslServerConfig.ApplicationProtocols
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => new SslApplicationProtocol(p.Trim()))
                    .ToList()
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
