# ProxyMapService 1.0.3 Release Notes

## Highlights

"Last configuration layer wins" semantics for sections that must be replaced as a whole. Previously the configuration binder merged all loaded configuration layers (appsettings.json, appsettings.Production.json, environment variables, ...) key by key, which could produce settings objects that combined values from different layers — values that were never meant to be used together.

## Bug Fixes

### `SslServerOptions` certificates are no longer merged across layers

When the base layer defined `ServerCertificate`/`CACertificate` by `Path` (e.g. appsettings.json) and an upper layer redefined the same certificate section by `StoreLocation`/`StoreName`/`Subject` (e.g. appsettings.Production.json), the bound certificate object contained values from both layers at once. Since a configured `Path` takes precedence when the certificate is loaded, the store-based settings from the upper layer were silently ignored.

Certificates are now bound only from the topmost (highest-priority) layer that defines the section: the whole `ServerCertificate`/`CACertificate` object is replaced, not merged. A new internal binding helper (`ConfigurationTopLayerBinder`) collects the keys of the topmost defining layer and binds the certificate from them alone; fields not present in that layer get their default values (`Path = null`, `StoreLocation = LocalMachine`, `StoreName = My`, etc.). This applies to both the global certificates and their `ServerCertificate` override counterparts.

### "Last layer wins" for `Authentication`, `HostRules` and `ProxyMappings`

The same cross-layer merging problem affected other root configuration sections:

* `Authentication` — if the base layer defined `Jwt` and `DevToken` and an upper layer redefined only `Jwt`, the merged object still contained the base `DevToken` section;
* `HostRules` and `ProxyMappings` — arrays are merged index by index by the standard binder, so entries from the base file could be mixed with entries from the environment-specific file.

These sections are now also taken entirely from the topmost layer that defines them:

* if `appsettings.Production.json` defines any of them, the base layer's value is ignored completely;
* if they are defined only in the base layer, the behavior is unchanged;
* if a section is defined nowhere, it is treated as empty (as before).

## Configuration Changes

### `DefaultProxyMappings` renamed to `ProxyMappings`

The `DefaultProxyMappings` fallback section (introduced in 1.0.1) is removed:

* in `appsettings.json` the section is renamed to `ProxyMappings` (its content — a `Bypass` listener on port 5001 with SSL decryption — is unchanged), so the service now always has a valid `ProxyMappings` configuration out of the box;
* the special-case code that loaded `DefaultProxyMappings` only when no additional `appsettings.*.json` layers were present is gone; with the new "last layer wins" binding the base `ProxyMappings` is automatically used whenever an environment-specific file does not override it.

Migration: if your own configuration files contain a `DefaultProxyMappings` section, rename it to `ProxyMappings`.

## Testing

* New unit test suite for the layer binding helper: certificate sections are replaced as a whole in both directions (file-based → store-based and store-based → file-based), certificates defined in a single layer bind correctly, `Authentication`/`HostRules`/`ProxyMappings` from an upper layer fully replace the base sections, and a base-only section keeps working.

## Documentation

* `appsettings.json`: `DefaultProxyMappings` renamed to `ProxyMappings`.
