# Proxy Map Service

## Описание

Proxy Map Service слушает входящие TCP-соединения на одном или нескольких портах и перенаправляет траффик на внешний, определенный в конфигурации (appsettings.json), прокси сервер.
Система правил (rules) позволяет блокировать соединения с некоторыми хостами или устанавливать соединение без использования внешнего прокси-сервера (это называется bypass).
В случае bypass сам Proxy Map Service является прокси сервером.

Это может быть нужно, чтобы:

1. Блокировать нежелательные соединения (например, отслеживание).
2. Уменьшать (экономить) траффик, проходящий через внешний (платный) прокси сервер.
3. Добавлять HTTP-заголовок Proxy-Authorization, если внешний прокси сервер требует авторизации, а клиентский софт такое не поддерживает.
4. Возвращать корректный код ошибки (407 Proxy Authentication Required), если авторизация требуется, а заголовок Proxy-Authorization отсутствует. Это позволяет исправлять некорректную работу некоторых прокси серверов, которые при отсуствии авторизации просто дропают соединение.
5. Пускать некоторый траффик, который залочен на IP вашего сервера (например, обращения к сервису решения капчи), минуя внешний прокси.

В целях безопасности Proxy Map Service слушает входящие соединения только на loopback интерфейсе (127.0.0.1).

Реализованы два API для просмотра статистики:

1. GET /ProxyStats

Пример ответа:

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

2. /ProxyStats/Hosts

Чтобы статистика для хостов считалась нужно включить настройку `"Enabled": true` в разделе `"HostStats"`.
А чтобы еще считался и траффик (`"bytesRead"`, `"bytesSent"`), то нужно включить `TrafficStats: true` там же.

Пример ответа:

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

## Настройка

Для использования API следует настроить раздел "Authentication" / "Jwt".
Сервис авторизации Jwt можно использовать этот: https://github.com/optinsoft/YregAuthService (установить как отдельное приложение ASP.NET на тот же сервер, что и ProxyMapService).

Пример appsettings.Production.json

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
                "SetHeader": false,
                "Username": "test",
                "Password": "test"
            },
            "ProxyServer": {
                "Host": "localhost",
                "Port": 8888
            }
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
        "TrafficStats": true
    }
}
```
