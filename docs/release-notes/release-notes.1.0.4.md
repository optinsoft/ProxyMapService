# ProxyMapService 1.0.4 Release Notes

## Highlights

Robustness improvements to the TLS record split feature (introduced in 1.0.2): split positions are now sorted and deduplicated, an invalid split specification no longer breaks the connection, and the splitter is guarded against buffer overflow. In addition, network disconnects during session handling are no longer logged as errors, and a new dedicated detailed-logging switch shows exactly how the SNI hostname is distributed over the resulting TLS records.

## Bug Fixes

### TLS record split positions are sorted and deduplicated

`TlsRecordSplitSpec` previously kept the resolved split positions in the order they were computed and only dropped *consecutive* duplicates. Positions that resolve out of order (e.g. `"0+e,1"`, where `0+e` lands near the end of the ClientHello while `1` lands near its start) could therefore produce records with overlapping or negative payload ranges. Positions are now sorted and deduplicated (`Distinct().OrderBy()`) before the record is re-chunked, so any combination of positions always yields a correct, non-overlapping record sequence.

### An invalid split specification no longer breaks the connection

`TlsRecordSplit` is parsed when the tunnel is created. In 1.0.3 a syntactically invalid value (e.g. a mistyped offset) threw a `FormatException` and terminated the session — in both MITM and passthrough modes. The specification is now parsed defensively:

* on a parse error a warning is logged — `TLS record split: invalid split specification '{Spec}', record splitting is disabled for this connection` (EventId 1319) — and the connection proceeds normally without splitting;
* the fix applies to both the MITM path and the passthrough split stream.

### `TlsRecordSplitStream` overflow guard

The splitter buffers the outgoing bytes until the complete first TLS record (the ClientHello) has been written by the TLS implementation. If the buffered data exceeded the buffer capacity (64 KB) before a complete record arrived, the wrapper would fail. It now flushes everything accumulated so far and passes the new data through unsplitted instead. In practice this cannot happen with a conforming TLS implementation (the maximum TLS record is 16 KB + headers), but the wrapper is now safe against it anyway.

### Network disconnects are no longer logged as errors

`IOException`/`SocketException` thrown by a handler step (a client or remote host disconnecting mid-session — a normal event) were logged as `Error (step {step}): ...`. They are now handled by a dedicated catch block, logged as a warning with the exception type name via the existing `LogExceptionWarning`, and the session terminates cleanly through the regular `Terminate` step. Genuinely unexpected exceptions are still logged as errors.

### Correct `ArgumentException` usage in header parsing

`Socks5Header` (SOCKS5 connect-request building) and `HostAddress.GetIPEndPoint` (DNS resolution) threw `ArgumentException`s with the offending value passed as the *paramName* argument, which produced misleading exception messages. The host name / address family is now included in the message itself: `Unable to retrieve address from specified host name: {hostname}`, `There is more than one IP address to the specified host: {hostname}`, `Invalid host address family: {family}`.

## New Features

### `DetailedLogging:LogSniSplitDetails` — SNI split layout logging

A new detailed-logging switch shows exactly how the SNI hostname of the ClientHello is distributed over the resulting TLS records after the split. When enabled, each split logs at Information level:

```
TLS record split: SNI 'example.com' (11 B at record offset 55..66) -> 2 record(s): #1 58 B [SNI 0..3 'exa'], #2 546 B [SNI 3..11 'mple.com']
```

* the SNI hostname with its length and record-relative offsets (5-byte record header included);
* the number of resulting records and, for each record, its payload size and the part of the SNI hostname (`from..to` in SNI-relative coordinates) that falls into it.

The option is enabled via `"DetailedLogging": { "LogSniSplitDetails": true }` in `appsettings.json` and is independent of `LogTlsHandshake`. The layout description is built defensively and never breaks the connection on failure. Related: the existing `TLS record split ({Spec}) applied to the ClientHello...` message was raised from Debug to Information level (still gated on `LogTlsHandshake`).

## Internal Changes

* `HostAddress.GetIPEndPoint`: if the hostname is an IP literal, it is used directly and DNS resolution is skipped entirely.
* `TlsRecordSplitStream` accepts an optional `ILogger` and reports the split layout through the new `TlsRecordSplitLog` helper (now public); it extracts the SNI hostname from the ClientHello and passes it to the logger together with the produced record segments.

## Configuration Changes

* `appsettings.json`: new `DetailedLogging:LogSniSplitDetails` switch (default `false`).
* `appsettings.json`: the default `ProxyMappings` preset (port 5001, `Bypass`) now ships with `"DecryptSSL": false` — out of the box the service runs in passthrough mode; SSL decryption stays opt-in via the documented settings.

## Other Changes

* New start scripts for production: `start_proxymapservice_prod.ps1` and `start_proxymapservice_prod.sh` (run the service with `--environment Production`); `start_proxymapservice_test.sh` now runs the test instance in the foreground (without `&`), matching the PowerShell variant.
