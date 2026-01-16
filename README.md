# Proxy Map Service

## Description

The Proxy Map Service listens for incoming TCP connections on one or more ports and redirects traffic to an external proxy server defined in the configuration (`appsettings.json`).
The rules system allows blocking connections to certain hosts or establishing connections without using an external proxy server (this is called **bypass**).
In bypass mode, the Proxy Map Service itself acts as the proxy server.

Proxy Map Service may be useful for:

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

### ProxyMapping

Name | Description | Type |
-----|-------------|------|
Listen | TCP listener settings | `Listen` (see description below) |
Authentication | Client authentication parameters | `Authentication` (see description below) |
ProxyServers | Lists of proxy servers | `ProxyServers` (see description below) |

### Listen

Defines the parameters for accepting TCP connections (port or port range, usage of proxy servers associated with the port, etc.).

Name | Description | Type | Default Value |
-----|-------------|------|---------------|
Port | TCP port. Example: `5001` | int | absent (`Port` or `PortRange` must be specified) |
PortRange | Port range (see `PortRange` description below) Examle: `{ "Start": 5001, "End": 5010 }` | `PortRange` | absent (`Port` or `PortRange` must be specified) |
RejectHttpProxy | Reject all HTTP (non-CONNECT) connections | bool | false |
StickyProxyLifetime | Direct traffic through a specific port to the same proxy server for `StickyProxyLifetime` minutes | int | 0 (select a new proxy for each connection) |

### PortRange

TCP port range.


Name | Description | Type | Example |
-----|-------------|------|---------|
Start | Port range start | int | 5001 |
End | Port range end | int | 5010 |

### Authentication

Client authentication parameters.

Name | Description | Type | Default Value |
-----|-------------|------|---------------|
Required | Require the `Proxy-Authorization` header; return `407` error if missing | bool | false |
Verify | Verify the `Proxy-Authorization` header (must be `Basic base64(user:pass)`) | bool | false |
Username | Username | string | "" (empty string) |
Password | Password | string | "" (empty string) |
SetAuthentication | Add/replace the `Proxy-Authorization` HTTP header | bool | false |
RemoveAuthentication | Delete the HTTP header `Proxy-Authorization` (it is ignored if `SetAuthentication` is set). | bool | false |
ParseUsernameParameters | Split the full username with parameters (e.g., `username-param1-value1-param2-value2`) into parts (delimiter: `-`) | bool | false |
UsernameParameters | Username parameters added during proxy server authentication (see `UsernameParameter` description below).  <details><summary>Note</summary>They are ignored if `SetAuthentication` = `false`, however, if `ParseUsernameParameters` = `false`, then the full username with parameters will be used for proxy server authentication.</details> | `List<UsernameParameter>` | absent |

### UsernameParameter

Name | Description | Type | Default Value | Example |
-----|-------------|------|---------------|---------|
Name | Parameter name | string | absent (required) | `session` |
Value | Parameter value. Can be input values or calculated values (see description below) | string | absent (required) | `$session` |
Default | Default value. Can be calculated values (see description below) | string? | null | `^[A-Za-z]{8}` |
SessionId | Specifies that this parameter is a session identifier | bool | false | true |
SessionTime | Specifies that this parameter is the session lifetime (in minutes). Overrides the value of `Listen.StickyProxyLifetime` | bool | false | true |

If `Value` starts with the `$` character, it refers to a parameter from the client's username (requires `Authentication.ParseUsernameParameters` to be set to `true`).  
For example, `Value`=`$session` refers to the `session` parameter and would be assigned the value `AAAA` if the client's username during authentication was `user-session-AAAA`.  
Another example: `Value`=`$sessionTime` refers to the `sessionTime` parameter, which does not exist in `user-session-AAAA`. In this case, a default value might be substituted, as specified in `Default`.

If `Value` or `Default` starts with the `^` character, it is a regular expression (RegEx) used to generate the value. Example: `^[A-Za-z]{8}`.

### ProxyServers

Name | Description | Type |
-----|-------------|------|
Items | List of proxy servers | `List<ProxyServer>` (see `ProxyServer` description below) |
Files | List of files with proxy servers | `List<ProxyServersFile>` (see `ProxyServerFile` description below) |

### ProxyServer

Name | Description | Type | Default Value |
-----|-------------|------|---------------|
Host | Proxy server host | string | absent (required) |
Port | Proxy server port | int | absent (required) |
ProxyType | Proxy server type. Possible values: Http, Socks4, Socks5 | string | Http |
Username | Proxy authentication username | string | "" (empty string) |
Password | Proxy authentication password | string | "" (empty string) |
UsernameParameters | Parameters added to the username during proxy server authentication. <details><summary>Note</summary>If `UsernameParameters` is present in `ProxyServer`, it will be used instead of `Authentication.UsernameParameters`, regardless of `Authentication.SetAuthentication`.</details> | `List<UsernameParameter>` | absent |

### ProxyServerFile

Name | Description | Type | Example |
-----|-------------|------|---------|
Path | Path to the file with proxy servers | string | `socks-proxy-servers.json` |

**Example** of the file with proxy servers `socks-proxy-servers.json`:

```json
{
    "Items": [
        {
            "Host": "127.0.0.1",
            "Port": 1080,
            "ProxyType": "Socks5",
        },
        {
            "Host": "127.0.0.1",
            "Port": 1081,
            "ProxyType": "Socks5",
        }
    ]
}
```

### HostRules

Traffic routing rules are defined in the `HostRules` section.

Name | Description | Type |
-----|-------------|------|
Items | List of routing rules | `List<HostRule>` (see `HostRule` description below) |
Files | List of files with routing rules | `List<HostRulesFile>` (see `HostRulesFile` description below) |

### HostRule

Name | Description | Type | Example |
-----|-------------|------|---------|
HostName | Host name | String | www.google.com |
Pattern | Regex pattern for host name | String | mozilla\\.(com\|org\|net)$ |
Action | Action: Allow (use proxy), Deny (block), or Bypass (direct connection) | String | Deny |
OverrideHostName | Override host name. Optional (null if not overriding the host name) | String| www.google.com |
OverrideHostPort | Override host port. Optional (null if not overriding the host port) | int | 81 |
ProxyServer | Use this proxy server when `Action`=`Allow`. Optional (null if use proxy server from `ProxyServers` array) | `ProxyServer` |  {"Host":"localhost", "Port":3128, "ProxyType":"Http"} |

Rules are processed in order. If multiple rules match a host, the last one applies. For example, to block all connections except `www.google.com`:

```json
"HostRules": {
    "Items": [
        {
            "Pattern": ".*",
            "Action": "Deny"
        },
        {
            "HostName": "www.google.com",
            "Action": "Allow"
        }
    ]
}
```

### HostRulesFile

Name | Description | Type | Example |
-----|-------------|------|---------|
Path | Path to the file with traffic routing rules | string | `fiddler-host-rules.json` |

**Example** of the file with traffic routing rules `fiddler-host-rules.json`:

```json
{
    "Items": [
        {
            "HostName": "www.google.com",
            "Action": "Allow",
            "ProxyServer": {
                "Host": "localhost",
                "Port": 8888,
                "ProxyType": "Http"
            }
        }
    ]
}
```

### Sample appsettings.Production.json:

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
                "Port": 5001,
                "RejectHttpProxy": true,
                "StickyProxyLifetime": 0
            },
            "Authentication": {
                "Required": false,
                "Verify": false,
                "SetAuthentication": false,
                "RemoveAuthentication": false,
                "ParseUsernameParameters": false,
                "Username": "test",
                "Password": "test",
                "UsernameParameters": [
                    {
                        "Name": "zone",
                        "Value": "$zone",
                        "Default": "custom"
                    },
                    {
                        "Name": "region",
                        "Value": "$region"
                    },
                    {
                        "Name": "session",
                        "Value": "$session",
                        "Default": "^[A-Za-z]{8}",
                        "SessionId": true
                    },
                    {
                        "Name": "sessTime",
                        "Value": "$sessTime",
                        "Default": "5",
                        "SessionTime": true
                    }
                ]
            },
            "ProxyServers": { 
                "Items": [
                    {
                        "Host": "localhost",
                        "Port": 3128,
                        "ProxyType": "Http"
                    }
                ],
                "Files": [
                    {
                        "Path": "socks-proxy-servers.json"
                    }
                ]
            }
        }
    ],
    "HostRules": {
        "Items": [
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
        "Files": [
            {
                "Path": "fiddler-host-rules.json"
            }
        ]
    },
    "HostStats": {
        "Enabled": true,
        "TrafficStats": true,
        "LogTrafficData": false
    },
    "HTTP": {
        "UserAgent": "proxymapper"
    }
}
```

## Credits

The proxy server code is based on (modified and enhanced from): https://github.com/agabani/PassThroughProxy
