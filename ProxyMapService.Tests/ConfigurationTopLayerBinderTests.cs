using Microsoft.Extensions.Configuration;
using ProxyMapService.Proxy.Configurations;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Tests
{
    public class ConfigurationTopLayerBinderTests
    {
        private static IConfiguration BuildConfiguration(string baseJson, string? overrideJson = null)
        {
            var builder = new ConfigurationBuilder();
            builder.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(baseJson)));
            if (overrideJson != null)
            {
                builder.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(overrideJson)));
            }
            return builder.Build();
        }

        [Fact]
        public void CACertificate_OverridesStoreSettings_FullyReplacesPathBasedSettings()
        {
            // Base layer: file-based certificate; top layer: store-based certificate.
            // The result must not mix the two: only the top layer's settings are used.
            var configuration = BuildConfiguration(
                """
                {
                  "SslServerOptions": {
                    "EnabledSslProtocols": "Tls12",
                    "CACertificate": {
                      "Path": "~/ca.p12",
                      "Password": "secret"
                    }
                  }
                }
                """,
                """
                {
                  "SslServerOptions": {
                    "CACertificate": {
                      "StoreLocation": "CurrentUser",
                      "StoreName": "Root",
                      "Subject": "CN=TestRoot"
                    }
                  }
                }
                """);

            var options = ConfigurationTopLayerBinder.GetSslServerOptions(configuration);

            Assert.Null(options.CACertificate.Path);
            Assert.Null(options.CACertificate.Password);
            Assert.Equal(StoreLocation.CurrentUser, options.CACertificate.StoreLocation);
            Assert.Equal(StoreName.Root, options.CACertificate.StoreName);
            Assert.Equal("CN=TestRoot", options.CACertificate.Subject);
            // Non-certificate options are still merged normally.
            Assert.Equal("Tls12", options.EnabledSslProtocols);
        }

        [Fact]
        public void CACertificate_OverridesPathSettings_FullyReplacesStoreBasedSettings()
        {
            var configuration = BuildConfiguration(
                """
                {
                  "SslServerOptions": {
                    "CACertificate": {
                      "StoreLocation": "LocalMachine",
                      "StoreName": "My",
                      "Subject": "CN=TestRoot"
                    }
                  }
                }
                """,
                """
                {
                  "SslServerOptions": {
                    "CACertificate": {
                      "Path": "~/ca.p12"
                    }
                  }
                }
                """);

            var options = ConfigurationTopLayerBinder.GetSslServerOptions(configuration);

            Assert.Equal("~/ca.p12", options.CACertificate.Path);
            Assert.Null(options.CACertificate.Subject);
            Assert.Equal(StoreLocation.LocalMachine, options.CACertificate.StoreLocation);
            Assert.Equal(StoreName.My, options.CACertificate.StoreName);
        }

        [Fact]
        public void CACertificate_DefinedOnlyInTopLayer_UsesTopLayer()
        {
            var configuration = BuildConfiguration(
                """
                {
                  "SslServerOptions": {
                    "EnabledSslProtocols": "Tls12,Tls13"
                  }
                }
                """,
                """
                {
                  "SslServerOptions": {
                    "CACertificate": {
                      "Subject": "CN=TestRoot"
                    }
                  }
                }
                """);

            var options = ConfigurationTopLayerBinder.GetSslServerOptions(configuration);

            Assert.Null(options.CACertificate.Path);
            Assert.Equal("CN=TestRoot", options.CACertificate.Subject);
            Assert.Equal("Tls12,Tls13", options.EnabledSslProtocols);
        }

        [Fact]
        public void CACertificate_DefinedOnlyInBaseLayer_UsesBaseLayer()
        {
            var configuration = BuildConfiguration(
                """
                {
                  "SslServerOptions": {
                    "CACertificate": {
                      "Path": "~/ca.p12"
                    }
                  }
                }
                """,
                """
                {
                  "SslServerOptions": {
                    "EnabledSslProtocols": "Tls12"
                  }
                }
                """);

            var options = ConfigurationTopLayerBinder.GetSslServerOptions(configuration);

            Assert.Equal("~/ca.p12", options.CACertificate.Path);
            Assert.Null(options.CACertificate.Subject);
            Assert.Equal("Tls12", options.EnabledSslProtocols);
        }

        [Fact]
        public void CACertificate_Undefined_YieldsDefaults()
        {
            var configuration = BuildConfiguration(
                """
                {
                  "SslServerOptions": {
                    "EnabledSslProtocols": "Tls12"
                  }
                }
                """);

            var options = ConfigurationTopLayerBinder.GetSslServerOptions(configuration);

            Assert.Null(options.CACertificate.Path);
            Assert.Null(options.CACertificate.Subject);
        }

        [Fact]
        public void ServerCertificate_IsAlsoReplacedAsWhole()
        {
            var configuration = BuildConfiguration(
                """
                {
                  "SslServerOptions": {
                    "ServerCertificate": {
                      "Path": "~/server.p12",
                      "Password": "secret"
                    }
                  }
                }
                """,
                """
                {
                  "SslServerOptions": {
                    "ServerCertificate": {
                      "Subject": "CN=TestServer"
                    }
                  }
                }
                """);

            var options = ConfigurationTopLayerBinder.GetSslServerOptions(configuration);

            Assert.Null(options.ServerCertificate.Path);
            Assert.Null(options.ServerCertificate.Password);
            Assert.Equal("CN=TestServer", options.ServerCertificate.Subject);
        }

        [Fact]
        public void Authentication_TopLayerReplacesBaseEntirely()
        {
            // Base defines Jwt + DevToken; the top layer redefines only Jwt.
            // The merged result must contain no traces of the base DevToken section.
            var configuration = BuildConfiguration(
                """
                {
                  "Authentication": {
                    "Jwt": {
                      "Enabled": false,
                      "Issuer": "base-issuer",
                      "Key": "base-key"
                    },
                    "DevToken": {
                      "Enabled": true
                    }
                  }
                }
                """,
                """
                {
                  "Authentication": {
                    "Jwt": {
                      "Enabled": true,
                      "Issuer": "prod-issuer"
                    }
                  }
                }
                """);

            var authentication = ConfigurationTopLayerBinder.GetTopLayerSection(configuration, "Authentication");

            var jwt = authentication.GetSection("Jwt");
            Assert.True(jwt.GetValue<bool>("Enabled"));
            Assert.Equal("prod-issuer", jwt.GetValue<string>("Issuer"));
            Assert.Null(jwt.GetValue<string>("Key"));

            // The base DevToken section must be gone entirely.
            var devToken = authentication.GetSection("DevToken");
            Assert.False(devToken.Exists());
            Assert.False(devToken.GetValue<bool>("Enabled"));
        }

        [Fact]
        public void Authentication_DefinedOnlyInBaseLayer_IsUsed()
        {
            var configuration = BuildConfiguration(
                """
                {
                  "Authentication": {
                    "Jwt": {
                      "Enabled": true,
                      "Issuer": "base-issuer"
                    }
                  }
                }
                """,
                """
                {
                  "Unrelated": 1
                }
                """);

            var authentication = ConfigurationTopLayerBinder.GetTopLayerSection(configuration, "Authentication");

            Assert.True(authentication.GetSection("Jwt").GetValue<bool>("Enabled"));
            Assert.Equal("base-issuer", authentication.GetSection("Jwt").GetValue<string>("Issuer"));
        }

        [Fact]
        public void HostRules_TopLayerReplacesBaseEntirely()
        {
            var configuration = BuildConfiguration(
                """
                {
                  "HostRules": {
                    "Items": [
                      { "HostName": "base-one.example.com", "Action": "Allow" },
                      { "HostName": "base-two.example.com", "Action": "Allow" }
                    ]
                  }
                }
                """,
                """
                {
                  "HostRules": {
                    "Items": [
                      { "HostName": "prod.example.com", "Action": "Allow" }
                    ]
                  }
                }
                """);

            var rules = new List<HostRule>();
            HostRules.LoadRulesList(rules, ConfigurationTopLayerBinder.GetTopLayerSection(configuration, "HostRules"));

            var item = Assert.Single(rules);
            Assert.Equal("prod.example.com", item.HostName);
        }

        [Fact]
        public void ProxyMappings_TopLayerReplacesBaseEntirely()
        {
            var configuration = BuildConfiguration(
                """
                {
                  "ProxyMappings": [
                    { "Listen": { "Port": 8080 } },
                    { "Listen": { "Port": 8081 } }
                  ]
                }
                """,
                """
                {
                  "ProxyMappings": [
                    { "Listen": { "Port": 9090 } }
                  ]
                }
                """);

            var proxyMappings = ConfigurationTopLayerBinder
                .GetTopLayerSection(configuration, "ProxyMappings")
                .Get<List<ProxyMapping>>();

            Assert.NotNull(proxyMappings);
            var mapping = Assert.Single(proxyMappings);
            Assert.Equal(9090, mapping.Listen.Port);
        }
    }
}
