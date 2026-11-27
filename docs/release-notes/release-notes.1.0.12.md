# ProxyMapService 1.0.12 Release Notes

## Improved client User-Agent detection (for `TlsClientType: "Auto"`)

The sticky session now keeps the client User-Agent in **two slots** and the effective value is `explicit ?? passive`:

* **explicit** — a User-Agent set via the Session API (`/session/new` with a top-level `UserAgent` key or a `?UserAgent=` query parameter);
* **passive** — the User-Agent detected from the last parsed HTTP request (HTTP/1.1 and HTTP/2, CONNECT and plain requests).

Behavior changes compared to 1.0.10/1.0.11:

* **A passive User-Agent is always current.** The passive slot is updated on every parsed request whose `User-Agent` is non-empty — there is no longer any "current value is not explicit" guard between passive updates; when an explicit User-Agent is set it simply takes precedence. Clients switching between browsers get the new fingerprint as soon as the next session is established. This also covers the Session API itself: a `/session/new` call without an explicit `UserAgent` no longer stores its own request header as an explicit value — the header is picked up by the same passive detection, so the effective value is the same.
* **The explicit User-Agent is now bound to the sticky session.** Rotating the session (`/session/new` without an explicit `UserAgent`) or `/session/reset` drops the explicit value, which falls back to the passively detected one. Previously an explicit value survived session rotations until it was replaced by another explicit value.
* **On sticky session expiry the last request's User-Agent carries over** to the new sticky session (per-connection `LastRequestUserAgent`), so the first connections of the fresh session already use the correct fingerprint instead of falling back to `Native` until a new request is observed.
* **Session info reports the effective User-Agent even for an expired session** (`GET /session/` → `user_agent`): previously it was `null` once the sticky session had expired.

## Other Changes

* Internal API simplified: `SessionManager.SetCurrentUserAgent` returns the effective value; `ClearCurrentUserAgent` was removed (explicit clearing happens in `ResetSessionId` and on expiry); `SessionContext.SetClientUserAgent` was replaced by settable `ClientUserAgent` and a new `LastRequestUserAgent` property.
* `SessionManagerUserAgentTests` updated to the new semantics (`ResetSessionId_ShouldResetUserAgent`).
