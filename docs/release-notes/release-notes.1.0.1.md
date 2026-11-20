# ProxyMapService 1.0.1 Release Notes

## Highlights

The service can now start with an empty `ProxyMappings` configuration. Instead of failing with a fatal error, it falls back to a built-in `DefaultProxyMappings` section — a plain bypass listener on port 5001 — and keeps the dashboard up and running so the configuration can be edited and the service restarted from the web UI.

## New Features

### `DefaultProxyMappings` — run without `ProxyMappings`

Previously, a missing or empty `ProxyMappings` section threw `NoMappingsException` and the service refused to start. The service now:

* logs a warning (`No proxy mappings`) and continues to run;
* uses the `DefaultProxyMappings` fallback configuration (defined in `appsettings.json` — a `Bypass` listener on port 5001 with SSL decryption) whenever no `ProxyMappings` section was provided.

This makes it possible to start the service "empty" and configure everything via the dashboard.

Note: the fallback is applied only when the effective configuration is the base `appsettings.json` (i.e. no additional `appsettings.*.json` layers were loaded). If an environment-specific settings file is present, it is expected to define `ProxyMappings` itself.

### Automatic publish archive

`dotnet publish` now automatically packages the publish output into a ZIP archive:

```
publish/ProxyMapService-v1.0.1[-<RID>].zip
```

The RID suffix is appended for RID-specific builds (e.g. `ProxyMapService-v1.0.1-win-x64.zip`). Archive creation can be disabled with:

```bash
dotnet publish ... -p:PublishArchive=false
```

## Bug Fixes

### Session API requires a non-empty domain

`SessionAPIConfig.Domain` is now mandatory for the Session API to be reachable: when the domain is empty, the host is never treated as a session API host (previously an empty domain made every request with no hostname match). The default value in `appsettings.json` is set to `proxymapper`, so the Session API hostname now has to be explicitly configured — requests addressed to any other hostname are no longer captured by the session API handler.

## Documentation

* `README.md` and `appsettings.json`: new `DefaultProxyMappings` section; `SessionAPI.Domain` default changed from `""` to `proxymapper`.
