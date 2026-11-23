# ProxyMapService 1.0.5 Release Notes

## Highlights

Two changes make session initialization stricter and more robust: the upstream connection target for HTTP requests is now determined strictly from the request-target — the `Host` header is no longer used as a fallback — and protocol detection validates the first byte of a connection before any HTTP header parsing is attempted, so non-HTTP traffic (e.g. TLS) is classified as an unknown protocol immediately instead of producing spurious parse failures.

## Behavior Changes

### The connection target is taken from the request-target, not from the `Host` header

When initializing an HTTP session, the destination of the upstream connection is now determined solely from the request-target of the request line (`HTTPTargetHost`): the absolute-form URI of a proxy request or the authority of a `CONNECT` request. The previous fallback to the `Host` header has been removed — the `Host` header is used for logging and for `SessionManager`/`Http1Http2Converter` purposes, but it never sets the connection target. If the request-target contains no host, the session is terminated with `400 Bad Request` and the `No target host` warning (EventId 1271, message renamed from `No host` to `No target host`).

### Protocol detection validates the first byte before HTTP header parsing

`ReadHeaderStream` no longer tries to parse HTTP headers for arbitrary incoming traffic. On the very first byte of a connection it classifies the protocol: byte `0x04` → SOCKS4, byte `0x05` → SOCKS5, any valid HTTP token character (`HttpParser.IsHttpTokenChar`, now public) → HTTP, anything else → `ProtocolTypeEnum.Unknown`. Traffic that is clearly not HTTP (e.g. a TLS ClientHello) is rejected as an unknown protocol immediately, instead of running through HTTP header parsing and failing later. The `SocksVersion` byte property was replaced by a `ProtocolType` enum (`Http`, `Socks4`, `Socks5`, `Unknown`).

If the header read then fails for the detected protocol (e.g. the stream closed before a complete header arrived), a dedicated warning is logged: `Failed to read {HTTP|SOCKS4|SOCKS5} header` (EventId 1269), and the session terminates cleanly instead of silently falling through.

## Internal Changes

* New log messages: `Failed to read {Protocol} header` (EventId 1269) and `Unknown protocol` (EventId 1270); `No host` renamed to `No target host` (EventId 1271).
* `ReadHeaderStream`: `SocksVersion` replaced by `ProtocolType` (`ProtocolTypeEnum`); header reading is gated on the detected protocol type.

## Other Changes

* `web.config`: explicit `<handlers>` registration for `aspNetCore` (`AspNetCoreModuleV2`) and a full `<aspNetCore>` element (`processPath=".\ProxyMapService.exe"`, `hostingModel="inprocess"`) so the service runs in-process under IIS.
