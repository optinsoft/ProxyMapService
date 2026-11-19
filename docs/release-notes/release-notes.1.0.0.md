# ProxyMapService 1.0.0 Release Notes

**Version:** 1.0.0
**Author:** Vitaly Yakovlev (optinosft)
**Copyright:** Vitaly Yakovlev, vitaly@optinosft.net

## Highlights

Per-port and per-host SSL/TLS configuration. The global `SslClientOptions` and `SslServerOptions` sections can now be refined — or completely replaced — for individual listening ports and for individual target hosts.

## New Features

### SSL options overrides on `Listen` and `HostRule`

Two new partial-override sections are available:

* `Listen.SslClientOptions` / `Listen.SslServerOptions` — apply to all traffic of a `ProxyMappings` listening port range;
* `HostRule.SslClientOptions` / `HostRule.SslServerOptions` — apply only to connections matching the host rule.

Precedence: global `SslClientOptions`/`SslServerOptions` → `Listen` override → `HostRule` override. Only the properties explicitly set in an override replace the inherited values; everything else is inherited from the level above. Host rule overrides are applied only when the rule matches and the rule action is not `Deny`.

Supported override properties:

* `SslClientOptions`: `EnabledSslProtocols`, `CheckCertificateRevocation`, `ApplicationProtocols`, `TrustedCAs`, `TlsClientType` (`Native` / `BoringSsl` / `Nss`);
* `SslServerOptions`: `EnabledSslProtocols`, `ClientCertificateRequired`, `CheckCertificateRevocation`, `ApplicationProtocols`, `ServerCertificate`, `CACertificate`.

Typical use cases:

```json
"Listen": {
    "Port": 8443,
    "SslServerOptions": {
        "EnabledSslProtocols": "Tls12,Tls13"
    }
}
```

```json
{
    "HostName": "example.com",
    "SslClientOptions": {
        "TlsClientType": "BoringSsl"
    },
    "SslServerOptions": {
        "ServerCertificate": {
            "Path": "certs/example.com.pfx",
            "Password": "secret"
        }
    }
}
```

### Store-based host certificates

`SslServerOptions.ServerCertificate` / `CACertificate` accept the full `SslCertificateConfig` shape, so a dedicated certificate for a host can now be loaded either from a file (`Path` + `Password`, `~` is expanded) or from a local certificate store (`StoreLocation` + `StoreName` + `Subject`).

## Improvements

* The trusted CA cache in `SslOptionsFactory` is now keyed by the TrustedCAs source definitions instead of the config instance, so per-session merged configuration clones reuse the same resolved CA certificates.
* Server-side SSL certificates are now pre-loaded once, single-threaded, at service start. This removes a lazy-initialization race between concurrent sessions and surfaces certificate configuration errors in the log immediately at startup instead of on the first session.
* `ProxyService` now implements `IDisposable` (declared on the `IProxyService` interface), so the DI container releases the server SSL options — including the loaded certificates — on application shutdown.

## Bug Fixes

* Fixed a lifetime bug in `ProxyService`: the `SslServerOptionsConfig` was disposed (via `using var`) as soon as `StartProxyMappingTasks()` returned, while sessions kept reading the lazily loaded server/CA certificates long afterwards. The configuration is now kept alive for the whole lifetime of the mapping tasks, is disposed on restart, and is disposed by the DI container on shutdown.

## Breaking Changes

### `HostRule.CertificatePath` / `HostRule.CertificatePassword` removed

The legacy per-host certificate properties (`CertificatePath`, `CertificatePassword` and the derived `ServerCertificate` getter) have been removed in favor of `HostRule.SslServerOptions.ServerCertificate`, which is more flexible (it also supports store-based certificates). Configuration files that still use the old properties will silently ignore them. Migrate:

```json
// before:
{
    "HostName": "example.com",
    "CertificatePath": "certs/example.com.pfx",
    "CertificatePassword": "secret"
}
// after:
{
    "HostName": "example.com",
    "SslServerOptions": {
        "ServerCertificate": {
            "Path": "certs/example.com.pfx",
            "Password": "secret"
        }
    }
}
```

## Documentation

* `README.md`: new section *Per-Port and Per-Host SSL Options Overrides* with examples and a migration note.
* `appsettings.Production-Example.json`: examples of `SslClientOptions` / `SslServerOptions` overrides inside `Listen`.
