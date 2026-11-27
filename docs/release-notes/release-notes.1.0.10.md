# ProxyMapService 1.0.10 Release Notes

## Highlights

This release adds the `Auto` mode for browser-like TLS fingerprinting: the upstream TLS ClientHello fingerprint is now picked **per session from the client (browser) User-Agent** — Firefox connections get the NSS (Firefox-like) ClientHello, Chrome-based browsers get the BoringSSL (Chrome-like) one, everything else falls back to the system TLS stack. The User-Agent can be set explicitly via the Session API or detected passively from the proxied traffic, and it is kept as part of the sticky session state.

## New Features

### `SslClientOptions:TlsClientType = "Auto"`

A fourth value of the existing `TlsClientType` option (alongside `Native`, `BoringSsl`, `Nss`). At the time of the upstream TLS handshake the effective implementation is resolved from the client User-Agent:

* Firefox → **Nss** (Firefox-like ClientHello);
* Chrome-based browsers — Chrome, Edge (`Edg/`), Opera (`OPR/`), Chromium → **BoringSsl** (Chrome-like ClientHello);
* anything else (Safari, curl, bots, …) → **Native** (system `SslStream`).

The User-Agent is resolved with the following priority:

1. the client User-Agent of the sticky session (see below);
2. the `User-Agent` header of the incoming request itself — for HTTP proxying this is the `CONNECT` request, which browsers send with a User-Agent;
3. no User-Agent known (e.g. a SOCKS connection without a sticky session) → **Native** with a fallback.

Note that the choice is per connection: the fingerprint is fixed once the upstream TLS handshake has completed, so changing the User-Agent affects new connections/sessions.

### Client User-Agent in the Session API

`/session/new` accepts an optional client User-Agent:

* `POST /session/new` — a top-level `UserAgent` key in the JSON body (next to `UsernameParameters`);
* `GET /session/new?UserAgent=...` — a `UserAgent` query parameter (it is **not** treated as a username parameter);
* when not given explicitly, the `User-Agent` header of the Session API request itself is used.

The current value is reported as `user_agent` in the session info (`GET /session/`).

**Explicit vs passive values.** An explicitly set User-Agent is never overwritten by detection: it is replaced only by another explicit value, cleared by `/session/reset`, or dropped when the sticky session expires. Without an explicit value the User-Agent is updated passively: every parsed HTTP request — HTTP/1.1 and HTTP/2, CONNECT and plain requests — whose `User-Agent` is non-empty updates the sticky session value. Passive detection therefore also covers browsers that never talk to the Session API: once any request from the browser has been seen, subsequent sessions (including SOCKS connections) inherit the detected User-Agent.

## Other Changes

* New `DetailedLogging:LogClientUserAgent` switch (off by default) gates the User-Agent resolution logging: the resolved `TlsClientType` per connection and the no-User-Agent fallback warning.
* `SessionContext.UserAgent` was renamed to `ConnectUserAgent` (the User-Agent sent in `CONNECT` requests when chaining to upstream proxies) to free the name for the client browser User-Agent. No behavior change.
* New unit tests: `TlsClientTypeResolverTests` (browser family detection, including UA spoofing with a Chrome string inside a Firefox User-Agent) and `SessionManagerUserAgentTests` (explicit/passive precedence, clearing).
* `README.md`: documented the `Auto` mode and the Session API `UserAgent`.

## Upgrade notes

* The bundled `appsettings.json` / `appsettings.IIS.json` / `appsettings.Production-Example.json` now ship with `"TlsClientType": "Auto"` instead of `"Native"`. Custom configurations keep their explicit values; set `"Auto"` manually to opt in.
* `Auto` produces browser-like fingerprints only when SSL decryption is enabled (same requirement as `BoringSsl`/`Nss`). Without a known User-Agent the connection silently uses the system TLS stack; enable `DetailedLogging:LogClientUserAgent` to see which choice was made for each connection.
