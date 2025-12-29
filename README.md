# Proxy Map Service

## Description

The Proxy Map Service listens for incoming TCP connections on one or more ports and redirects traffic to an external proxy server defined in the configuration (`appsettings.json`).
The rules system allows blocking connections to certain hosts or establishing connections without using an external proxy server (this is called **bypass**).
In bypass mode, the Proxy Map Service itself acts as the proxy server.

This may be useful for:

1. Blocking unwanted connections (e.g., tracking).
2. Reducing (saving) traffic passing through an external (paid) proxy server.
3. Adding the Proxy-Authorization HTTP header if the external proxy server requires authentication but the client software does not support it.
4. Returning the correct error code (`407 Proxy Authentication Required`) if authentication is required but the `Proxy-Authorization` header is missing. This fixes incorrect behavior of some proxy servers that simply drop the connection when authentication is absent.
5. Allowing certain traffic that is blocked by your server’s IP (e.g., captcha-solving services) to bypass the external proxy.

For security, the Proxy Map Service only listens for incoming connections on the `loopback interface` (127.0.0.1).

## API for Viewing Statistics

1. GET /ProxyStats

**Example** response:

```json
{
  "serviceInfo": "Service created at 4/10/2025 5:17:42 PM",
  "currentTime": "4/11/2025 5:59:07 PM",
  "sessionsCount": 0,
  "authenticationNotRequired": 0,
  "authenticationRequired": 0,
  "authenticated": 0,
  "authenticationInvalid": 0,
  "httpRejected": 0,
  "headerFailed": 0,
  "noHost": 0,
  "hostRejected": 0,
  "hostProxified": 0,
  "hostBypassed": 0,
  "proxyConnected": 0,
  "proxyFailed": 0,
  "bypassConnected": 0,
  "bypassFailed": 0,
  "totalBytesRead": 0,
  "totalBytesSent": 0,
  "proxyBytesRead": 0,
  "proxyBytesSent": 0,
  "bypassBytesRead": 0,
  "bypassBytesSent": 0
}
```

2. GET /ProxyStats/Hosts

To enable host statistics, set `"Enabled": true` in the `"HostStats"` section. To also track traffic (`"bytesRead"`, `"bytesSent"`), set `"TrafficStats": true`.

**Example** response:

```json
{
  "hosts": [
    {
      "hostName": "www.site1.com",
      "requestsCount": 12497,
      "proxified": false,
      "bypassed": true,
      "bytesRead": 3339709226,
      "bytesSent": 28481155
    },
    {
      "hostName": "www.site2.com",
      "requestsCount": 9515,
      "proxified": true,
      "bypassed": false,
      "bytesRead": 48793736,
      "bytesSent": 63114037
    }
  ]
}
```

## Install IIS Components

The **.NET 8.0 Hosting Bundle** must be installed.
You can download it here: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

You also need to enable the **Application Initialization** module.
This module allows IIS to automatically warm up the site and start the ASP.NET Core application immediately after the application pool starts, without waiting for the first HTTP request.
This is required to ensure that background services start automatically after the server or IIS is restarted.

** How to enable Application Initialization **:

1. Open **Server Manager**.
2. Select **Add Roles and Features**.
3. Navigate to: `Web Server (IIS) → Web Server → Application Development`.
4. Check the box for **Application Initialization**.
5. Complete the installation and restart IIS if necessary.

## Build and Install ProxyMapService

1. Build and Publish the application to `bin\Release\net8.0\publish`.
2. Create a directory for the app (e.g., `ProxyMapService`) in `C:\inetpub\wwwroot`.
3. Copy the files from `bin\Release\net8.0\publish` into this directory.
4. Create `appsettings.Production.json` and configure it (see the `Configuration` section below).
5. In IIS, create an application pool (e.g., `PortMapPool`) with the following settings:
    * **Start Mode** = **Always Running**
    * **Idle Timeout (minutes)** = **0**.
6. Convert the `C:\inetpub\wwwroot\ProxyMapService` directory into an application in IIS, assigning it the `PortMapPool` app pool.
7. In the application’s `Advanced Settings`, enable:
    * **Preload Enabled** = **True**.

## Configuration

Settings are configured in `appsettings.json` (or `appsettings.Production.json for production`).

### JWT Authentication (for API Access)

To use the API, configure the `Authentication.Jwt` section. 
You can use this JWT authorization service: https://github.com/optinsoft/YregAuthService (you can install it as a separate ASP.NET app on the same server as `ProxyMapService`).

Path | Description | Type | Default Value |
-----|-------------|------|---------------|
Authentication.Jwt.Enabled | Require authentication for API access | bool | false |
Authentication.Jwt.Issuer | JWT token issuer | string | "" (empty string) |
Authentication.Jwt.Audience | JWT token audience (URL) | string | "" (empty string) |
Authentication.Jwt.Key | JWT token verification key | string | "" (empty string) |

### ProxyMappings

Open connections, authentication rules, and proxy server mappings are defined in the `ProxyMappings` section.

Path | Description | Type | Default Value |
-----|-------------|------|---------------|
ProxyMappings[].Listen.Port | TCP port | int | 5000 |
ProxyMappings[].Listen.RejectHttpProxy | Reject all HTTP (non-CONNECT) connections | bool | false |
ProxyMappings[].Authentication.Required | Require the `Proxy-Authorization` header; return `407` error if missing | bool | false |
ProxyMappings[].Authentication.Verify | Verify the `Proxy-Authorization` header (must be `Basic base64(user:pass)`) | bool | false |
ProxyMappings[].Authentication.Username | Username | string | "" (empty string) |
ProxyMappings[].Authentication.Password | Password | string | "" (empty string) |
ProxyMappings[].Authentication.SetAuthentication | Add/replace the `Proxy-Authorization` header | bool | false |
ProxyMappings[].ProxyServers | Array of proxy servers | List<ProxyServer> | [] |

### ProxyServer

Path | Description | Type | Default Value |
-----|-------------|------|---------------|
Host | Proxy server host | string | Missing (required field) |
Port | Proxy server port | int | Missing (required field) |
ProxyType | Proxy server type. Possible values: Http, Socks4, Socks5 | string | Http |
Username | Proxy authentication username | string | "" (empty string) |
Password | Proxy authentication password | string | "" (empty string) |

### HostRules

Traffic routing rules are defined in the `HostRules` section.

Path | Description | Type | Example |
-----|-------------|------|---------|
HostRules[].Pattern | Regex pattern for host name | String | "mozilla\\.(com\|org\|net)$" |
HostRules[].Action | Action: Allow (use proxy), Deny (block), or Bypass (direct connection) | String | Deny |
HostRules[].OverrideHostName | Override host name. Optional (null if not overriding the host name) | String| "www.google.com" |
HostRules[].OverrideHostPort | Override host port. Optional (null if not overriding the host port) | int | 81 |
HostRules[].ProxyServer | Use this proxy server when `Action`=`Allow`. Optional (null if use proxy server from ProxyMappings[].ProxyServers array) | ProxyServer |  {"Host":"localhost", "Port":8888, "ProxyType":"Http"} |

Rules are processed in order. If multiple rules match a host, the last one applies. For example, to block all connections except `www.google.com`:

```json
{
    "HostRules": [
        {
            "Pattern": ".*",
            "Action": "Deny"
        },
        {
            "Pattern": "www\\.google\\.com$",
            "Action": "Allow"
        }
    ]
}
```

**Example** `appsettings.Production.json`

```json
{
    "Authentication": {
        "Jwt": {
            "Enabled": true,
            "Issuer": "mysite.com",
            "Audience": "mysite.com",
            "Key": "MY_SECRET_KEY"
        }
    },
    "ProxyMappings": [
        {
            "Listen": {
                "Port": 5000,
                "RejectHttpProxy": true
            },
            "Authentication": {
                "Required": false,
                "Verify": false,
                "SetAuthentication": false,
                "Username": "test",
                "Password": "test"
            },
            "ProxyServers": [
                {
                    "Host": "localhost",
                    "Port": 8888,
                    "ProxyType": "Http"
                }
            ]
        }
    ],
    "HostRules": [
        {
            "Pattern": "mozilla\\.(com|org|net)$",
            "Action": "Deny"
        },
        {
            "Pattern": "firefox\\.com$",
            "Action": "Deny"
        },
        {
            "Pattern": "^s\\.yimg\\.com$",
            "Action": "Bypass"
        }
    ],
    "HostStats": {
        "Enabled": true,
        "TrafficStats": true,
        "LogTrafficData": false
    }
}
```

## Credits

The proxy server code is based on (modified and enhanced from): https://github.com/agabani/PassThroughProxy
