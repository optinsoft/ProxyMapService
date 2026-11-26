# ProxyMapService 1.0.9 Release Notes

## Highlights

This release reworks the **Session API WebSocket subscriptions**: each subscription now has its own `sub_id`, one connection can hold up to 50 subscriptions, and any connection subscribed to a session receives `url_matched` events for every matching request. The WebSocket handler was hardened (stable JSON error codes, fragmented message reassembly, message size limit, ReDoS protection) and covered with integration tests. The Session API itself now works over SOCKS4/SOCKS5 as well, and its replies keep the connection alive.

## New Features

### Subscription ids (`sub_id`)

`subscribe` now returns a unique subscription id, and every `url_matched` event carries the `sub_id` of the subscription that matched:

```json
{"status":"subscribed","session_id":"AbCdEfGh","sub_id":"3f2a9c..."}
{"event_type":"url_matched","timestamp":"...","sub_id":"3f2a9c...","session_id":"AbCdEfGh","method":"GET","url":"https://example.com/path"}
```

A new `unsubscribe` action removes a single subscription:

* `{"action":"unsubscribe","sub_id":"3f2a9c..."}` → `{"status":"unsubscribed","sub_id":"..."}`
* unknown or foreign `sub_id` → `{"error":"unknown_sub_id","sub_id":"..."}` (a connection cannot unsubscribe someone else's subscription)
* missing / non-string `sub_id` → `{"error":"missing_sub_id"}`
* closing the connection removes all of its subscriptions automatically.

### Multiple subscriptions per connection

Previously one connection could hold only a single subscription (the last `subscribe` replaced the previous one). Now a connection may register **up to 50 subscriptions**, each with its own pattern:

* two subscriptions matching the same URL deliver two events;
* unsubscribing one subscription keeps the others working;
* exceeding the limit → `{"error":"too_many_subscriptions"}`.

Several WebSocket connections can subscribe to the same session; each receives the events independently, and closing one connection does not affect the others.

A subscription is bound to the session that was current when the connection was established: after `/session/new` rotates the session, old connections keep receiving events for the old session, and a newly opened connection picks up the new one.

### Session API over SOCKS4 / SOCKS5

The Session API host (`SessionAPI:Domain`) is now also recognized on connections accepted via the SOCKS4 and SOCKS5 protocols, not only plain HTTP. The API host is resolved from the `Host` header when present, falling back to the connection target.

### Mode column in the dashboard

The HTTP traffic viewer shows the mapping mode — **Forward** or **Reverse** — in a new `Mode` column and in the transaction details panel.

## Hardening of the Session WebSocket handler

* All replies are built with `JsonSerializer` (snake_case) instead of string interpolation: quotes/backslashes in an exception message can no longer produce invalid JSON. Failures return stable error codes: `missing_action`, `unknown_action`, `invalid_json`, `invalid_pattern`, `internal_error`.
* Fragmented WebSocket messages are reassembled (read until `EndOfMessage`, decode UTF-8 after the whole message is received). Messages over **64 KB** close the socket with `MessageTooBig`, non-text frames close it with `InvalidMessageType`.
* Sends are serialized per connection (`WsConnection` with a `SemaphoreSlim`): the handler and the event loop could previously send concurrently, which threw `InvalidOperationException` and silently dropped the subscription.
* Client-supplied subscription regexes run with a **100 ms match timeout** (ReDoS protection); regex timeouts are logged and skip the event.
* Premature disconnects and cancellations are logged instead of surfacing as unhandled exceptions.
* Subscription state is kept in a thread-safe index grouped by session, so event distribution looks up matching subscriptions without scanning all of them.

## Fixes

* **URL in `url_matched` events** is now built from the `Host` header verbatim (dropping only the default `:80`/`:443` port) instead of `UriBuilder`, which mangled hosts; IPv6 literals are bracketed correctly, and a failure to build the URL is logged instead of aborting the request handling.
* **Keep-alive for Session API and file server**: replies of the Session API endpoints (`/`, `/session/`, `/session/new`, `/session/reset`, `/session/certificate`) and of the file server no longer terminate the connection — subsequent requests on the same connection are served (new `HandleStep.Continue`).
* **File server**: a request with an incomplete body (peer closed the connection mid-body) is now answered with `400 Bad Request` instead of being processed as complete.

## Other Changes

* Dashboard: hidden columns are persisted in `localStorage` (`httpTrafficHiddenColumns`) instead of visible ones, so columns added in newer versions show up by default. **Note:** previously saved column settings are not migrated — the first launch resets the column visibility to defaults.
* `state_machine.md`: added the missing `ReverseProxyActionHandler` transitions (`HttpBypass` → `HttpBypassHandler`, `HttpFile` → `HttpFileHandler`, `Proxy` → `ProxyHandler`, `Terminate`).
* New integration tests for the Session API WebSocket (`SessionSubscriptionTests`): subscribe/unsubscribe, pattern filtering, session binding and rotation, multiple subscriptions and connections, subscription limits, and protocol robustness (malformed commands, fragmented/binary/oversized messages).

## Upgrade notes

* Dashboard column settings: the localStorage key changed from `httpTrafficVisibleColumns` to `httpTrafficHiddenColumns`; users who customized columns will see all columns after the update until they hide the unwanted ones again.
* WebSocket clients: use the `sub_id` from the `subscribed` reply to unsubscribe; a plain `subscribe` now adds a subscription instead of replacing the existing one.
