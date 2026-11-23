# ProxyMapService 1.0.6 Release Notes

## Highlights

This release introduces a new **reverse proxy mode**: a listening port (or port range) can now forward all its traffic to a fixed target host configured in the mapping, instead of acting as a forward proxy. Along the way, mixed TLS/plaintext combinations (HTTPS client → HTTP upstream and HTTP client → HTTPS upstream) were fixed — they now work with and without SSL decryption — and TLS detection settings became configurable.

## New Features

### Reverse proxy mode (`Listen.Mode = Reverse`)

A new `Mode` field in the `Listen` section selects the proxying direction: `Forward` (the default, the classic behavior — the client names the target host itself) or `Reverse`. The absence of the field means `Forward`, so existing configurations remain fully backward compatible. In reverse mode the target host is fixed in the mapping and is not taken from the client's traffic; host rules and client proxy protocols (SOCKS, forward proxying) do not apply.

The target and related settings are configured in a new optional `ReverseProxy` mapping section:

```json
{
    "Listen": {
        "Port": 8443,
        "Mode": "Reverse",
        "Action": "Bypass",
        "SslMode": "Auto",
        "UpstreamSslMode": "Auto"
    },
    "ReverseProxy": {
        "Target": {
            "Host": "internal.example.com",
            "Port": 443
        },
        "ServerName": null
    }
}
```

* **`Target.Host` / `Target.Port`** — the fixed upstream target. If `Target.Port` is not set (`0`), the default port is derived from the upstream connection scheme: `443` when the connection to the target is TLS, `80` otherwise (with `UpstreamSslMode: Auto` the client side TLS-ness is used as the fallback, mirroring the tunnel heuristics).
* **`ServerName`** — optional SNI override for the upstream TLS handshake (`SessionContext.UpstreamServerName`); it affects only the TLS ClientHello/TargetHost, not the connect endpoint. `null` — `Target.Host` is used.

How the connection to the target is established is defined by the existing `Listen.Action`, as in forward mode — all values are meaningful:

| Action | Meaning in reverse mode |
|---|---|
| `Bypass` | direct connection to the target |
| `Proxy` / `Allow` | connection to the target through the `ProxyServers` chain |
| `File` | static file server serving from `ProxyMapping.RootDir` (no upstream) |
| `Deny` | incoming connections are rejected |

`RootDir` is a new mapping-level property (in forward mode a `HostRule.RootDir` can still override it per host, as before). With `Action: Deny` the session is rejected and is not registered in the session manager.

### Protocol detection in reverse mode never loses bytes

The incoming client protocol is the target server's protocol, so it is detected without consuming bytes: the first byte of the connection is *peeked* (TLS detection with the configured peek timeout; the peeked record is cached and reused by `TunnelHandler` for its own detection, MITM and TLS record splitting). TLS and opaque traffic go straight into a tunnel; only a confirmed HTTP request is actually read and parsed — in that case the existing HTTP handlers take over, so request rewriting, cache, HTTP error replies and file serving work exactly as in forward mode. Traffic that is neither TLS nor HTTP terminates the session (reverse ports are not expected to carry other protocols).

### Configurable TLS detection (`TlsDetection`)

New configuration section `TlsDetection` with `ClientHelloPeekTimeoutMs` (default `500`): how long to wait for the full TLS ClientHello record after the first byte (`0x16`) has been peeked from the incoming stream. If the record does not arrive within this time, the connection is treated as non-TLS. The setting is read once at startup.

## Behavior Changes

### Mixed TLS/plaintext tunneling works with and without `DecryptSSL`

Previously, TLS streams were created only when `DecryptSSL` was enabled, so the mixed combinations were broken without it: a TLS client's bytes were forwarded raw into a plaintext upstream (and plaintext into a TLS upstream), and a plain client's request sent to a TLS upstream was written into the socket before the TLS handshake, corrupting it. Now:

* A TLS stream is created on a side whenever its security differs from the other end, regardless of `DecryptSSL`: TLS is terminated on the incoming side (HTTPS client → HTTP upstream) or initiated toward the upstream (HTTP client → HTTPS upstream). A pure passthrough (both ends TLS, no MITM) is unchanged.
* A plaintext upstream cannot receive HTTP/2, so when the incoming side is TLS-terminated, the client is kept on HTTP/1.1.
* The H1→H2 translation now also starts for plaintext clients: a plain HTTP/1.1 client talking to a TLS upstream that negotiated h2 works (this combination was broken even with `DecryptSSL`).
* A request aimed at the target host is no longer written into the socket before the upstream TLS handshake: request-sending handlers (`Bypass`, `Proxy`, SOCKS) stash the rewritten request in `SessionContext.PendingOutgoingBytes` (via the new `HttpProto.SendTargetHttpRequest`), and `TunnelHandler` sends it right after the handshake — raw for an h1 upstream, prepended to the tunnel source via the new `PendingPrefixTunnelStream` for the H1→H2 translation. `CONNECT` requests addressed to a proxy server still travel in plaintext, as before.

### `GET /` no longer returns 404 in File mode

`FileRequestHandler` rejected the root path because on Unix `Uri.TryCreate("/", UriKind.Absolute)` succeeds (the path becomes a `file://` URI), so `GET /` never served `index.html`. Absolute-URI detection now looks for a real scheme (`://`). This also fixes the root path for file host rules in forward mode.

## Internal Changes

* New `HandleStep` values: `ReverseProxy` and `ReverseProxyHttp`.
* New log messages: `Reverse proxy mode on port {Port} is not configured...` (EventId 1320) and `Reverse proxy on port {Port}: Action = File requires a parsed HTTP request...` (EventId 1321).
* New types: `ModeEnum`, `ReverseProxy`, `ReverseProxyTarget`, `TlsDetectionConfig`, `PendingPrefixTunnelStream`, `ReverseProxyActionHandler`; new configuration overrides `SslOptionsFactory`/`TunnelHandler` use for the upstream SNI (`SessionContext.UpstreamServerName`).
* `CountingStream`: dead commented-out TLS detection code removed; the peek timeout is taken from `TlsDetectionConfig`.

## Other Changes

* `appsettings.json`: new `TlsDetection` section.
* `appsettings.Production-Example.json`: an example reverse proxy mapping added.
* Unit tests: configuration binding for the new reverse proxy fields (`Mode`, `RootDir`, `ReverseProxy`) added to `ConfigurationTopLayerBinderTests`.
