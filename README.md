# Proxy Map Service

The Proxy Map Service listens for incoming TCP connections on one or more ports and redirects traffic to an external proxy server defined in the configuration (`appsettings.json`). The rules system allows blocking connections to certain hosts or establishing connections without using an external proxy server (this is called bypass). In bypass mode, the Proxy Map Service itself acts as the proxy server.

Proxy Map Service may be useful for:
* **Blocking unwanted connections** (e.g., tracking).
* **Reducing (saving) traffic** passing through an external (paid) proxy server.
* **Adding the Proxy-Authorization HTTP header** if the external proxy server requires authentication but the client software does not support it.
* **Returning the correct error code** (`407 Proxy Authentication Required`) if authentication is required but the `Proxy-Authorization` header is missing. This fixes incorrect behavior of some proxy servers that simply drop the connection when authentication is absent.
* **Allowing certain traffic** that is blocked by your server’s IP (e.g., captcha-solving services) to bypass the external proxy.

For security, the Proxy Map Service only listens for incoming connections on the loopback interface (`127.0.0.1`).

---

## Download / Git Clone

Clone the repository from GitHub using the following command:

```bash
git clone https://github.com/optinsoft/ProxyMapService.git
cd ProxyMapService
```

## Build

The project requires .NET 8.0 SDK. To build a self-contained executable for Windows x64, run the following publish command:

```bash
dotnet publish .\ProxyMapService\ProxyMapService.csproj -c Release -r win-x64
```

After a successful build, the executable `ProxyMapService.exe` will be located in the following directory:
`ProxyMapService\bin\Release\net8.0\win-x64\publish\`

## Configuration

Application settings are managed via `appsettings.json`. Instead of modifying the main configuration file directly, it is highly recommended to create an `appsettings.Production.json` file in the same directory and place your custom overrides there.

For your convenience, a sample file named `appsettings.Production-Example.json` is available in the `publish` directory. This file demonstrates how to properly configure the `ProxyMappings` rules. You can simply rename this file to `appsettings.Production.json` and modify it according to your needs.

## Run

Navigate to the publish directory and launch the executable:

```bash
ProxyMapService.exe
```

Upon startup, the console will output security tokens and dashboard access information similar to this:

```text
DEVELOPMENT TOKEN GENERATED: b0a24910a0884e08b3899a75748c120a
Dashboard URL: http://localhost:5000/ProxyMapDashboard/?token=b0a24910a0884e08b3899a75748c120a
```

Open the provided **Dashboard URL** in your web browser to access the management interface.

## Dashboard

The ProxyMap Dashboard consists of three main tabs:
* **Stats**: Displays general service information (uptime), active **LISTEN PORTS** (port `5001` is open by default as a proxy port in `appsettings.json`), data transfer counters (bytes sent/received), and current session details.
* **Event Log**: Displays internal system logs. Click **Resume Capture** to start viewing live log messages.
* **HTTP Traffic**: Displays HTTP requests passing through the service. Click **Resume Capture** to enable live traffic monitoring.

---

## Quick Start (Testing with Google Chrome)

To quickly verify your setup, configure your web browser to route traffic through the Proxy Map Service on port `5001`. 

For Google Chrome, you can create a dedicated desktop shortcut with specific launch arguments:

1. Create a temporary profile folder on your drive (e.g., `C:\Temp\chrome\proxymap`).
2. Run Chrome from the command line or via a shortcut with the following parameters:

```bash
"C:\Program Files\Google\Chrome\Application\chrome.exe" --proxy-server="http://127.0.0.1:5001" --user-data-dir="C:\Temp\chrome\proxymap"
```

3. Open any website in this newly launched Chrome window.
4. Check the **ProxyMap Dashboard**. You will immediately see live logs in the **Event Log** tab and captured traffic in the **HTTP Traffic** tab.

> **Note**: At this stage, secure connections will only display basic tunneling information:
> `CONNECT | 200 Connection established` to targets like `://google.com`. Detailed HTTP headers and request bodies will not be visible yet because SSL decryption is currently disabled.

---

## Turning on SSL Decryption

By default, HTTPS traffic is forwarded without inspection. To view detailed HTTP headers and request/response bodies for secure connections, you must enable SSL decryption.

### Step 1: Install the Root SSL Certificate

On its very first launch, the Proxy Map Service automatically generates a root certificate authority (CA) in the user's home directory under the `.proxymap` folder (e.g., `C:\Users\Username\.proxymap\`).

This directory contains three files:
* `ProxyMapService-ca.p12` (Contains both the certificate and the private key)
* `ProxyMapService-ca-cert.cer` (Public certificate only)
* `ProxyMapService-ca-cert.pem` (PEM-formatted certificate)

You must install **`ProxyMapService-ca.p12`** because it contains the private key required for encryption. 

To install the certificate on Windows:
1. Double-click the `ProxyMapService-ca.p12` file to open the Certificate Import Wizard.
2. Select the desired Store Location (**Current User** or **Local Machine**) and click **Next**.
3. Verify the file path and click **Next**.
4. When prompted for a password, **leave the password field blank** (the private key has no password).
5. Check the box **"Enable all extended properties"** and click **Next**.
6. On the Certificate Store page, choose **"Place all certificates in the following store"**.
7. Click **Browse**, select **"Trusted Root Certification Authorities"**, and click **OK**.
8. Complete the wizard and confirm any security warnings to trust the certificate.

### Step 2: Enable SSL Decryption in Settings

1. If you haven't already, copy `appsettings.json` and name it `appsettings.Production.json`.
2. Open `appsettings.Production.json` in any text editor.
3. Locate the `ProxyMappings` section, find the `Listen` configuration, and change the `DecryptSSL` value from `false` to `true`:

```json
"ProxyMappings": [
	{
		"Listen": {
			"Port": 5001,
			"RejectHttpProxy": false,
			"StickyProxyLifetime": 0,
			"Action": "Bypass",
			"DecryptSSL": true,
			"SslMode": "Auto",
			"UpstreamSslMode": "Auto",
			"IgnoreHostRules": false
		},
		"Authentication": {
			"Required": false,
			"Verify": false,
			"SetAuthentication": false,
			"RemoveAuthentication": false,
			"ParseUsernameParameters": false,
			"Username": "user",
			"Password": "pass"
		}
	}
]
```

### Step 3: Verify Decryption

1. Restart the `ProxyMapService.exe` application.
2. Note that a new **Development Token** is generated on every application startup. 
3. Copy the new **Dashboard URL** from the console and open it in your browser.
4. Go to the **HTTP Traffic** tab and click **Resume Capture**.
5. Launch your pre-configured Google Chrome proxy instance and browse any HTTPS website (e.g., `https://google.com`).

The dashboard will now display full HTTP details instead of basic generic `CONNECT` tunnels, **allowing you to see and monitor completely decrypted traffic.**

---

## Listening Address

By default, the proxy listeners are bound to the loopback interface (`127.0.0.1`), so the proxy is reachable only from the local machine. The `IPAddress` option in the `Listen` section of a `ProxyMappings` entry controls which network interface the port(s) are opened on. The setting is applied per mapping, so different mappings can listen on different interfaces.

Supported values:

* `"Loopback"` / `"Localhost"` — loopback interface, `127.0.0.1` (default when the option is omitted);
* a concrete IP address — e.g. `"192.168.1.10"` (a specific interface) or `"::1"` (IPv6 loopback);
* `"*"` / `"Any"` — all interfaces (bound to `::`); IPv6 dual-mode is explicitly enabled on the listener socket, so both IPv4 and IPv6 connections are accepted;
* `"Any6"` — all IPv6 interfaces only (bound to `::`, IPv6-only socket, no IPv4 mapping);
* `"Any4"` — all IPv4 interfaces only (`0.0.0.0`).

Example — open the port on all interfaces:

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

An invalid value is logged as a warning and the listener falls back to the loopback interface.

When binding to a non-loopback address, make sure the proxy is not exposed to untrusted networks: consider enabling authentication (`Authentication` section) and review the `Action` setting for the mapping.

---

## Per-Port and Per-Host SSL Options Overrides

The global `SslClientOptions` (outgoing TLS connections) and `SslServerOptions` (incoming TLS connections when decryption is enabled) can be refined at two levels:

* **`Listen`** — inside a `ProxyMappings` entry, applies to all traffic of that listening port range (e.g. a different `EnabledSslProtocols` set for a specific port);
* **`HostRule`** — inside the `HostRules` section, applies only to connections to the matching host (e.g. a dedicated server certificate for a particular host).

Precedence: global `SslClientOptions`/`SslServerOptions` → `Listen.SslClientOptions`/`Listen.SslServerOptions` → `HostRule.SslClientOptions`/`HostRule.SslServerOptions`. Only the properties explicitly set in an override replace the inherited values; everything else is inherited from the level above. For `HostRule` the override is applied only when the rule matches the host and the rule's action is not `Deny`.

Example — stricter protocols for a specific listening port and a dedicated certificate for one host:

```json
"ProxyMappings": [
	{
		"Listen": {
			"Port": 5001,
			"DecryptSSL": true,
			"SslServerOptions": {
				"EnabledSslProtocols": "Tls12,Tls13"
			}
		}
	}
],
"HostRules": {
	"Items": [
		{
			"HostName": "example.com",
			"SslClientOptions": {
				"TlsClientType": "BoringSsl"
			},
			"SslServerOptions": {
				"ServerCertificate": {
					"Path": "certs/example.com.pfx",
					"Password": "secret"
				}
			}
		}
	]
}
```

The override properties mirror the corresponding global section:

* `SslClientOptions` override: `EnabledSslProtocols`, `CheckCertificateRevocation`, `ApplicationProtocols`, `TrustedCAs`, `TlsClientType`;
* `SslServerOptions` override: `EnabledSslProtocols`, `ClientCertificateRequired`, `CheckCertificateRevocation`, `ApplicationProtocols`, `ServerCertificate` (path/password or store-based `SslCertificateConfig`), `CACertificate`.

The legacy `HostRule`-level `CertificatePath`/`CertificatePassword` properties have been removed in favor of `SslServerOptions.ServerCertificate`, which is more flexible (it also supports store-based certificates via `StoreLocation`/`StoreName`/`Subject`). Migrate old configurations as follows:

```json
// before:
{
    "HostName": "example.com",
    "CertificatePath": "certs/example.com.pfx",
    "CertificatePassword": "secret"
}
// after:
{
    "HostName": "example.com",
    "SslServerOptions": {
        "ServerCertificate": {
            "Path": "certs/example.com.pfx",
            "Password": "secret"
        }
    }
}
```

---

## Editing HTTP Headers (RequestHeaders / ResponseHeaders)

The proxy can edit HTTP headers on the fly: add or replace headers sent to the upstream server (`RequestHeaders`, e.g. to inject an `Authorization` header for upstream authentication) and headers sent back to the client (`ResponseHeaders`, e.g. to strip `Server` or `X-Powered-By`).

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
					"Cookie"
				]
			}
		}
	]
}
```

Notes:

* `Set` replaces every existing header with the same name (case-insensitively) and appends the new value; `Remove` deletes every header with the given name (case-insensitively).
* `Set` values support `${VARIABLE}` placeholders substituted with **environment variables** (e.g. `"Bearer ${API_TOKEN}"`). A missing variable is substituted with an empty string; CR/LF characters are stripped from the resulting value to prevent header injection.
* Only plain HTTP headers can be edited: HTTP/2 pseudo-headers (`:method`, `:path`, `:authority`, `:status`, ...) are never touched, and transport framing headers (`Content-Length`, `Transfer-Encoding`) must not be modified — the body tracking would break.
* Supported for HTTP/1.1 in both directions, for HTTP/2 requests (h2 → h2 header rewriting), for responses translated from HTTP/2 to HTTP/1.1 and for responses served from the cache. For h2 → h2 sessions **response** header rules are not applied yet (the responses are forwarded as is) — use the h1 → h2 translation or an HTTP/1.1 upstream if response editing is required; in TLS passthrough mode (no decryption) headers are not edited at all.

---

## Chrome-like and Firefox-like TLS Fingerprint

When SSL decryption is enabled, the Proxy Map Service terminates the client's TLS connection and opens its own TLS connection to the target server. By default, this upstream connection is made with the standard .NET `SslStream`, whose TLS handshake is easily recognizable as non-browser traffic — some servers and anti-bot systems flag such connections.

Proxy Map Service can make the upstream TLS handshake **indistinguishable from a real browser's** — either **Google Chrome** or **Mozilla Firefox**. For Chrome it uses [BoringSslStream](https://github.com/optinsoft/BoringSslStream), a TLS stream backed by BoringSSL (the TLS library used by Chromium); for Firefox it uses [NssStream](https://github.com/optinsoft/NssStream), backed by NSS (the TLS library used by Firefox). The chosen implementation replicates the browser's ClientHello byte-for-byte, including cipher suites, extensions, GREASE, post-quantum key groups and ALPN/ALPS, so the connection gets a genuine browser JA4/JA3 fingerprint:

```text
SslStream:        t13d311100_e8f1e7e78f70_f6eeaf393d2e   (default .NET TLS handshake)
BoringSslStream:  t13d1517h2_8daaf6152771_cb7bf5808d99   (Chrome-like TLS handshake)
NssStream:        t13d1616h2_86a278354501_0c27189014cf   (Firefox-like TLS handshake)
```

### How to Enable

Browser-like fingerprinting requires SSL decryption to be enabled (`DecryptSSL: true`, see [Turning on SSL Decryption](#turning-on-ssl-decryption)). Then set `TlsClientType` to `"BoringSsl"` for Chrome or `"Nss"` for Firefox in the `SslClientOptions` section of `appsettings.Production.json`:

```json
"SslClientOptions": {
    "EnabledSslProtocols": "Tls12,Tls13",
    "CheckCertificateRevocation": true,
    "ApplicationProtocols": "http/1.1,h2",
    "TlsClientType": "BoringSsl"
}
```

Restart the `ProxyMapService.exe` application for the change to take effect.

### Notes

* The `TlsClientType` option (`Native` — default `SslStream`, `BoringSsl` — Chrome-like, `Nss` — Firefox-like) only affects the **outgoing (upstream)** TLS connections of decrypted traffic. It does not change how the proxy accepts client connections.
* Because BoringSslStream and NssStream replicate the browser's ClientHello, the `EnabledSslProtocols` setting does not apply to them — the TLS versions and cipher suites are chosen exactly as the browser chooses them.
* `TlsClientType: "Nss"` uses [NssStream](https://github.com/optinsoft/NssStream) — an NSS-backed TLS stream producing a Firefox-like ClientHello. On Linux the native binary links dynamically against the system NSS/NSPR libraries (`libnss3`, present on virtually every distribution); on Windows the required NSS/NSPR DLLs are shipped inside the NuGet package and are copied to the output directory automatically.
* The Firefox-like ClientHello produced by NssStream depends on the version of the linked NSS: features unsupported by the linked NSS version (ECH GREASE, ML-KEM `X25519MLKEM768`, ML-DSA signature algorithms, brotli) are silently skipped. For example, on NSS 3.98 the JA4 is `t13d1616h2_86a278354501_0c27189014cf`, while on NSS 3.101+ it includes ML-KEM and therefore differs.
* The negotiated application protocol (ALPN) still follows the `ApplicationProtocols` setting, so HTTP/2 to the server keeps working as before.
* Server certificate validation settings (`CheckCertificateRevocation`, `TrustedCAs`, and the `IgnoreCertificateErrors` mapping option) remain fully effective with BoringSslStream and NssStream.
* The shipped NuGet packages include the native BoringSSL and NSS/NSPR binaries for Windows x64, Linux x64 and macOS ARM64, so no extra dependencies are required.

### Verifying the Fingerprint

The repository includes an integration test that captures the TLS ClientHello sent to the upstream server and computes its JA4 fingerprint. Run the test proxy instance, then:

```bash
dotnet test ProxyMapService.Tests --filter "FullyQualifiedName~Ja4Fingerprint" -l "console;verbosity=detailed"
```

The test output prints the JA4 fingerprint of the upstream ClientHello (e.g., `t13d1517h2_8daaf6152771_cb7bf5808d99` with `TlsClientType: "BoringSsl"` — Chrome-like, or the NSS-produced fingerprint with `TlsClientType: "Nss"` — Firefox-like), along with the full list of offered cipher suites and extensions.

To check the JA4 fingerprint of a TLS library directly (as `BoringSslStream` or `NssStream` would produce it, without running the proxy), use the `BoringSslJa4Probe` and `NssJa4Probe` console utilities:

```bash
dotnet run --project BoringSslJa4Probe   # Chrome-like: t13d1517h2_8daaf6152771_cb7bf5808d99
dotnet run --project NssJa4Probe         # Firefox-like: t13d1616h2_86a278354501_0c27189014cf
```

Each utility starts a local capture server, performs a TLS handshake against it (BoringSSL-backed or NSS-backed respectively), and prints the JA4 fingerprint of the ClientHello along with the full lists of cipher suites and extensions. The BoringSslStream fingerprint is deterministic (it replicates Chrome's ClientHello byte-for-byte); the NssStream fingerprint depends on the version of the linked NSS library.

---

## TLS Record Split (ClientHello Fragmentation)

Some DPI systems classify HTTPS traffic by inspecting the first outgoing TLS record — the ClientHello, which carries the SNI hostname in plaintext. Proxy Map Service can split this first TLS record into several separate TLS records (fragmentation at the TLS protocol level): the resulting records carry the same content type and version as the original record and together reassemble into the identical payload, so any RFC-conforming TLS server accepts them unchanged.

### How to Enable

Set the `TlsRecordSplit` option in the `Listen` section of a `ProxyMappings` entry:

```json
"ProxyMappings": [
	{
		"Listen": {
			"Port": 5001,
			"TlsRecordSplit": "1,0+sm"
		}
	}
]
```

The value is a comma-separated list of split positions, each in the syntax `offset[:repeats:skip][+flag1[flag2]]`:

* `offset` — split position relative to the start of the ClientHello (the TLS record payload, without the 5-byte record header); a negative offset without flags counts from the end of the ClientHello;
* `repeats:skip` — optional; repeat the split `repeats` times with a `skip` byte step (e.g. `1:3:5` splits at positions 1, 6 and 11);
* flags:
  * `+s` — offset from the start of the SNI hostname;
  * `+e` — offset from the end of the ClientHello (with `+s` — from the end of the SNI hostname);
  * `+m` — offset from the middle of the ClientHello (with `+s` — from the middle of the SNI hostname);
  * `+n` — zero offset (the position stays absolute).

Examples:

* `"1"` — split after the first byte of the ClientHello;
* `"3+s"` — split 3 bytes after the start of the SNI hostname;
* `"0+sm"` — split in the middle of the SNI hostname;
* `"-5"` — split 5 bytes before the end of the ClientHello.

The setting can be overridden per host via a `HostRule` (`"TlsRecordSplit": "..."` next to `DecryptSSL`); when the rule does not set it, the `Listen` value is inherited.

### Notes

* The split is applied to the **outgoing (upstream)** TLS connection and works in both modes:
  * with SSL decryption enabled (`DecryptSSL: true`) — the proxy generates the ClientHello itself (see [Chrome-like and Firefox-like TLS Fingerprint](#chrome-like-and-firefox-like-tls-fingerprint)) and the split is applied underneath the TLS implementation;
  * in passthrough mode (no decryption) — the client's own ClientHello travels through the tunnel and is re-chunked on the fly.
* Each resulting TLS record is written separately, so DPI sees them as distinct packets rather than one reassembled ClientHello.
* Only the first TLS record (the ClientHello) is split; all subsequent records pass through unchanged.
* If the specification resolves to nothing to split (e.g. all positions reference the SNI hostname but the record has none, or a position falls on the record boundary), the record is sent as-is.
* When the split is active, the proxy logs it at debug level (enable TlsHandshakeLog to see it).
