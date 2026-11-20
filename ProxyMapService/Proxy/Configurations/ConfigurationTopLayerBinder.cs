using Microsoft.Extensions.Configuration;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Configurations
{
    /// <summary>
    /// Binding helpers that resolve the "last layer wins" problem for sections that
    /// must be replaced as a whole instead of merged key by key.
    ///
    /// The standard configuration binder merges all configuration layers
    /// (appsettings.json, appsettings.Production.json, environment variables, ...)
    /// key by key. As a result an object defined in several layers may combine values
    /// that were never meant to be used together (e.g. a certificate defined by Path
    /// in the base layer and by StoreLocation/StoreName/Subject in the override layer),
    /// and arrays are merged index by index. These helpers bind such sections using
    /// only the data present in the topmost (highest-priority) layer that defines them.
    /// </summary>
    public static class ConfigurationTopLayerBinder
    {
        /// <summary>
        /// Binds SslServerOptions from the given configuration, making sure that
        /// ServerCertificate and CACertificate are taken only from the topmost
        /// configuration layer that defines them (no cross-layer merging of
        /// Path-based and store-based certificate settings).
        /// </summary>
        public static SslServerOptionsConfig GetSslServerOptions(IConfiguration configuration)
        {
            var options = configuration.GetSection("SslServerOptions").Get<SslServerOptionsConfig>()
                ?? new SslServerOptionsConfig();
            options.ReplaceServerCertificate(BindSslCertificateTopLayer(configuration, "SslServerOptions:ServerCertificate"));
            options.ReplaceCACertificate(BindSslCertificateTopLayer(configuration, "SslServerOptions:CACertificate"));
            return options;
        }

        /// <summary>
        /// Binds a single certificate section using only the keys present in the
        /// topmost configuration layer that defines that section.
        /// Returns a default (empty) config when the section is not defined anywhere.
        /// </summary>
        public static SslCertificateConfig BindSslCertificateTopLayer(IConfiguration configuration, string sectionPath)
        {
            var result = new SslCertificateConfig();
            var section = GetTopLayerSection(configuration, sectionPath);
            if (!section.Exists())
            {
                return result;
            }
            var value = section["Path"];
            if (value != null)
            {
                result.Path = value;
            }
            value = section["Password"];
            if (value != null)
            {
                result.Password = value;
            }
            value = section["Subject"];
            if (value != null)
            {
                result.Subject = value;
            }
            if (section["StoreLocation"] != null)
            {
                result.StoreLocation = section.GetValue<StoreLocation?>("StoreLocation") ?? StoreLocation.LocalMachine;
            }
            if (section["StoreName"] != null)
            {
                result.StoreName = section.GetValue<StoreName?>("StoreName") ?? StoreName.My;
            }
            return result;
        }

        /// <summary>
        /// Returns a configuration section that contains only the data of the topmost
        /// (highest-priority) configuration layer defining the given section path.
        /// Values from lower layers are ignored completely, so the section is replaced
        /// as a whole instead of being merged key by key (and arrays are not merged
        /// index by index). The returned section is never null; when the section is
        /// not defined in any layer it is simply empty.
        /// </summary>
        public static IConfigurationSection GetTopLayerSection(IConfiguration configuration, string sectionPath)
        {
            var data = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (configuration is IConfigurationRoot root)
            {
                // Providers are ordered from lowest to highest priority, so the first
                // provider (in reverse) that defines the section is the topmost one.
                foreach (var provider in root.Providers.Reverse())
                {
                    var leafPaths = new List<string>();
                    CollectProviderLeafPaths(provider, sectionPath, "", leafPaths);
                    if (leafPaths.Count == 0)
                    {
                        continue;
                    }
                    foreach (var leafPath in leafPaths)
                    {
                        provider.TryGet(leafPath, out var value);
                        // Skip a scalar defined exactly at the section path itself:
                        // such a value cannot be bound to a complex section anyway.
                        if (leafPath.Length > sectionPath.Length)
                        {
                            // Keys are stored with the section prefix so that the returned
                            // root exposes the section at the same path as the source.
                            data[leafPath] = value;
                        }
                    }
                    break;
                }
            }
            else
            {
                // Non-root configuration: there is only one layer, take every leaf.
                CollectSectionLeafPaths(configuration.GetSection(sectionPath), sectionPath, data);
            }
            return new ConfigurationBuilder()
                .AddInMemoryCollection(data)
                .Build()
                .GetSection(sectionPath);
        }

        /// <summary>
        /// Collects the absolute key paths of the leaves defined by a single provider
        /// under the given prefix. A leaf is a key that has no children in that provider.
        /// </summary>
        private static void CollectProviderLeafPaths(IConfigurationProvider provider, string prefix, string relativePath, List<string> into)
        {
            var childKeys = provider.GetChildKeys(Enumerable.Empty<string>(), prefix.Length == 0 ? null : prefix);
            foreach (var key in childKeys)
            {
                var childPrefix = prefix.Length == 0 ? key : prefix + ":" + key;
                var childRelative = relativePath.Length == 0 ? key : relativePath + ":" + key;
                CollectProviderLeafPaths(provider, childPrefix, childRelative, into);
            }
            if (!childKeys.Any() && relativePath.Length > 0)
            {
                into.Add(prefix);
            }
        }

        /// <summary>
        /// Collects the absolute key paths and values of the leaves of a merged section
        /// (used only when the configuration is not an IConfigurationRoot).
        /// </summary>
        private static void CollectSectionLeafPaths(IConfigurationSection section, string prefix, Dictionary<string, string?> into)
        {
            var children = section.GetChildren().ToList();
            foreach (var child in children)
            {
                var childPrefix = prefix.Length == 0 ? child.Key : prefix + ":" + child.Key;
                CollectSectionLeafPaths(child, childPrefix, into);
            }
            if (children.Count == 0 && prefix.Length > 0)
            {
                into[prefix] = section.Value;
            }
        }
    }
}
