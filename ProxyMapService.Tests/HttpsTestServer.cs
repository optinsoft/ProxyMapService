using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;

namespace ProxyMapService.Tests
{
    public sealed class HttpsTestServer : IAsyncDisposable
    {
        public const int Port = 5111;

        private readonly IHost _host;

        private int _connectionCount;
        private int _requestCount;

        public int ConnectionCount =>
            Volatile.Read(ref _connectionCount);

        public int RequestCount =>
            Volatile.Read(ref _requestCount);

        public HttpsTestServer(bool useHttp2)
        {
            var certificate = CreateCertificate();

            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.Listen(
                            IPAddress.Loopback,
                            Port,
                            listenOptions =>
                            {
                                listenOptions.Protocols = useHttp2 ? HttpProtocols.Http1AndHttp2 : HttpProtocols.Http1;
                                listenOptions.UseHttps(certificate);
                            });

                        options.Limits.KeepAliveTimeout =
                            TimeSpan.FromSeconds(30);
                    });

                    webBuilder.Configure(app =>
                    {
                        app.Use(async (context, next) =>
                        {
                            Interlocked.Increment(ref _requestCount);

                            await next();
                        });

                        app.Run(async context =>
                        {
                            switch (context.Request.Path)
                            {
                                case "/":
                                    await context.Response.WriteAsync(
                                        "Hello from HTTPS test server");
                                    break;

                                case "/one":
                                    await context.Response.WriteAsync(
                                        "Response one");
                                    break;

                                case "/two":
                                    await context.Response.WriteAsync(
                                        "Response two");
                                    break;

                                case "/three":
                                    await context.Response.WriteAsync(
                                        "Response three");
                                    break;

                                case "/echo":
                                    using (var reader =
                                        new StreamReader(context.Request.Body))
                                    {
                                        var body =
                                            await reader.ReadToEndAsync();

                                        await context.Response.WriteAsync(body);
                                    }

                                    break;

                                default:
                                    context.Response.StatusCode = 404;

                                    await context.Response.WriteAsync(
                                        "Not found");
                                    break;
                            }
                        });
                    });
                })
                .Build();
        }

        public Task StartAsync(
            CancellationToken cancellationToken = default)
        {
            return _host.StartAsync(cancellationToken);
        }

        public Task StopAsync(
            CancellationToken cancellationToken = default)
        {
            return _host.StopAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _host.Dispose();

            return ValueTask.CompletedTask;
        }

        private static X509Certificate2 CreateCertificate()
        {
            using var rsa = RSA.Create(2048);

            var request = new CertificateRequest(
                "CN=localhost",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    false,
                    false,
                    0,
                    true));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature,
                    true));

            var san = new SubjectAlternativeNameBuilder();

            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);

            request.CertificateExtensions.Add(san.Build());

            var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddHours(1));

            return new X509Certificate2(
                certificate.Export(X509ContentType.Pfx));
        }
    }
}
