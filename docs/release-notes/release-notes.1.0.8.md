# ProxyMapService 1.0.8 Release Notes

## Highlights

This release adds **HTTP header editing**: the proxy can now add, replace and remove HTTP headers on the fly — both in requests sent to the upstream server (`RequestHeaders`) and in responses returned to the client (`ResponseHeaders`). The typical use case is upstream authentication: inject an `Authorization` header (e.g. `Bearer ${API_TOKEN}`) for the proxied traffic instead of configuring every client.

## New Features

### Editing HTTP headers (`RequestHeaders` / `ResponseHeaders`)

Each rule set consists of two parts:

* `Set` — a map of header names to values. Every existing header with the same name (compared case-insensitively) is removed and the new value is appended. Values support `${VARIABLE}` placeholders substituted with **environment variables** (a missing variable becomes an empty string), so secrets can be kept outside the configuration file. CR/LF characters are stripped from the resolved values to prevent header injection.
* `Remove` — a list of header names (case-insensitive) to delete.

The rules are configured at three levels, and the levels are merged:

* **global** — the `HTTP` section (`HTTP:RequestHeaders` / `HTTP:ResponseHeaders`);
* **`Listen`** — inside a `ProxyMappings` entry, applies to all traffic of that listening port range;
* **`HostRule`** — inside the `HostRules` section, applies only to the matching host (applied when the rule matches and its action is not `Deny`).

Precedence: global → `Listen` → `HostRule`. When merging, `Set` is merged by key (a higher level replaces values of the same keys and adds new ones) and the `Remove` lists are combined.

Example — global rules plus a per-host `Authorization` injection:

```json
"ProxyMappings": [
    {
        "Listen": {
            "Port": 5001,
            "DecryptSSL": true
        }
    }
],
"HTTP": {
    "RequestHeaders": {
        "Set": {
            "X-Gateway": "ProxyMapService"
        },
        "Remove": [
            "Proxy-Authorization"
        ]
    },
    "ResponseHeaders": {
        "Remove": [
            "Server",
            "X-Powered-By"
        ]
    }
},
"HostRules": {
    "Items": [
        {
            "HostName": "api.example.com",
            "RequestHeaders": {
                "Set": {
                    "Authorization": "Bearer ${API_TOKEN}"
                },
                "Remove": [
                    "Cookie",
                    "Proxy-Authorization"
                ]
            },
            "ResponseHeaders": {
                "Remove": [
                    "Server",
                    "X-Powered-By"
                ]
            }
        }
    ]
}
```

### Where the rules are applied

| Direction | Path | Support |
|---|---|---|
| Request → upstream | HTTP/1.1 through the proxy (forward proxy, bypass, SOCKS4/5 with HTTP) | yes |
| Request → upstream | subsequent pipelined HTTP/1.1 requests and HTTP/1.1 → HTTP/2 translation | yes |
| Request → upstream | HTTP/2 → HTTP/2 (decrypted MITM traffic) | yes |
| Response → client | HTTP/1.1 responses and HTTP/2 → HTTP/1.1 responses | yes |
| Response → client | responses served from the cache | yes (rules are applied when the cached reply is served) |
| Response → client | HTTP/2 → HTTP/2 responses | not yet (responses are forwarded as is) |

Notes:

* Only plain HTTP headers are edited: HTTP/2 pseudo-headers (`:method`, `:path`, `:authority`, `:status`, ...) are never touched, and transport framing headers (`Content-Length`, `Transfer-Encoding`) should not be modified — body tracking would break.
* In TLS passthrough mode (no decryption) headers are not edited at all — the traffic is opaque to the proxy.
* The `Set` rules are not applied to the CONNECT request itself, only to real requests to the target.

## Other Changes

* `appsettings.Production-Example.json`: the `HTTP` section now shows example `RequestHeaders` / `ResponseHeaders` rules.
* `README.md`: new **Editing HTTP Headers (RequestHeaders / ResponseHeaders)** section documenting the rules, the merging precedence and the protocol limitations.
* New unit tests: merging of the rule levels, `Set`/`Remove` behavior for HTTP/1.1 header lines and HTTP/2 header blocks, environment variable placeholders, CR/LF stripping (header injection guard).
