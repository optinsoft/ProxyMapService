# ProxyMapService 1.0.2 Release Notes

## Highlights

TLS record splitting. The service can now split the first outgoing TLS record (the ClientHello, which carries the plaintext SNI hostname) into several separate TLS records — a fragmentation technique useful against DPI systems that classify HTTPS traffic by inspecting the first record as a single unit.

## New Features

### TLS Record Split (ClientHello Fragmentation)

The split is configured via the new `TlsRecordSplit` option on `Listen` (and can be overridden per host via `HostRule`). The value is a comma-separated list of split positions, each in the syntax `offset[:repeats:skip][+flag1[flag2]]`:

* `offset` — position relative to the start of the ClientHello (the TLS record payload, without the 5-byte record header); a negative offset without flags counts from the end;
* `repeats:skip` — optional; repeat the split `repeats` times with a `skip` byte step (e.g. `1:3:5` splits at positions 1, 6 and 11);
* flags:
  * `+s` — offset from the start of the SNI hostname;
  * `+e` — offset from the end of the ClientHello (with `+s` — from the end of the SNI hostname);
  * `+m` — offset from the middle of the ClientHello (with `+s` — from the middle of the SNI hostname);
  * `+n` — zero offset (the position stays absolute).

Examples:

```json
"Listen": {
    "Port": 5001,
    "TlsRecordSplit": "1,0+sm"
}
```

* `"1"` — split after the first byte of the ClientHello;
* `"3+s"` — split 3 bytes after the start of the SNI hostname;
* `"0+sm"` — split in the middle of the SNI hostname;
* `"-5"` — split 5 bytes before the end of the ClientHello.

The split works in both modes:

* with SSL decryption enabled (`DecryptSSL: true`) — the proxy generates the ClientHello itself, and the split is applied underneath the TLS implementation;
* in passthrough mode (no decryption) — the client's own ClientHello travels through the tunnel and is re-chunked on the fly.

Each resulting TLS record is written separately, so DPI sees them as distinct packets rather than one reassembled ClientHello. Only the first TLS record is split; all subsequent records pass through unchanged. If the specification resolves to nothing to split (e.g. all positions reference the SNI hostname but the record has none, or a position falls on the record boundary), the record is sent as-is. When the split is active, the proxy logs it at debug level (visible with TlsHandshakeLog enabled).

## Internal Changes

* New `ITunnelStream` contract (a Stream plus the disconnect-handling members): tunnels no longer depend on the concrete `CountingStream` type, which allows wrapper streams such as the record splitter to act as tunnel destinations in passthrough mode without duplicating byte counting.

## Documentation

* `README.md`: new "TLS Record Split (ClientHello Fragmentation)" section describing the `TlsRecordSplit` option, its syntax and usage in both MITM and passthrough modes.
