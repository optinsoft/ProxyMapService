# ProxyMapService 1.0.7 Release Notes

## Highlights

This release makes the **listening network address configurable**: the proxy listeners no longer have to be bound to the loopback interface. A new `Listen.IPAddress` option lets each `ProxyMappings` entry open its port(s) on a specific network interface, on all interfaces, or keep the previous loopback-only behavior.

## New Features

### Configurable listening address (`Listen.IPAddress`)

Previously, every listener was hard-bound to `127.0.0.1` (`IPAddress.Loopback`), so the proxy was always reachable from the local machine only. The new optional `IPAddress` field in the `Listen` section of a `ProxyMappings` entry controls which network interface the listening port (or port range) is bound to:

```json
"ProxyMappings": [
    {
        "Listen": {
            "Port": 5001,
            "IPAddress": "Any",
            "Action": "Bypass",
            "DecryptSSL": false
        }
    }
]
```

Supported values:

| Value | Meaning |
|---|---|
| `"Loopback"` / `"Localhost"` | loopback interface, `127.0.0.1` |
| *omitted* / `null` | loopback interface (default, backward compatible) |
| a concrete IP address | the specified interface only — e.g. `"192.168.1.10"` or `"::1"` (IPv6 loopback) |
| `"*"` / `"Any"` | all interfaces; the listener socket works in IPv6 dual-mode and accepts both IPv4 and IPv6 connections |
| `"Any4"` | all IPv4 interfaces only (`0.0.0.0`) |

The setting is applied per mapping, so different mappings can listen on different interfaces (e.g. one port for local tooling on `Loopback` and another for the network on a specific address). An invalid value is logged as a warning (`Invalid Listen.IPAddress value '...'`) and the listener falls back to the loopback interface.

**Security note:** binding to a non-loopback address exposes the proxy to the network. Consider enabling authentication (`Authentication` section) and review the `Listen.Action` setting for such mappings.

## Other Changes

* `appsettings.json`: the default `ProxyMappings` entry now shows the new `"IPAddress": "Loopback"` option.
* `appsettings.Production-Example.json`: both example mappings (forward and reverse) include `"IPAddress": "Loopback"`.
* `README.md`: new **Listening Address** section documenting the option, its supported values and security considerations.
