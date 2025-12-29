# Proxy Map Service

## Описание

Proxy Map Service слушает входящие TCP-соединения на одном или нескольких портах и перенаправляет трафик на внешний, определенный в конфигурации (appsettings.json), прокси сервер.
Система правил (rules) позволяет блокировать соединения с некоторыми хостами или устанавливать соединение без использования внешнего прокси-сервера (это называется **bypass**).
В случае bypass сам Proxy Map Service является прокси сервером.

Это может быть нужно, чтобы:

1. Блокировать нежелательные соединения (например, отслеживание).
2. Уменьшать (экономить) трафик, проходящий через внешний (платный) прокси сервер.
3. Добавлять HTTP-заголовок Proxy-Authorization, если внешний прокси сервер требует авторизации, а клиентский софт такое не поддерживает.
4. Возвращать корректный код ошибки (407 Proxy Authentication Required), если авторизация требуется, а заголовок Proxy-Authorization отсутствует. Это позволяет исправлять некорректную работу некоторых прокси серверов, которые при отсутствии авторизации просто дропают соединение.
5. Пускать некоторый трафик, который залочен на IP вашего сервера (например, обращения к сервису решения капчи), минуя внешний прокси.

В целях безопасности Proxy Map Service слушает входящие соединения только на loopback интерфейсе (127.0.0.1).

## API для просмотра статистики

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

2. GET /ProxyStats/Hosts

Чтобы статистика для хостов считалась нужно включить настройку `"Enabled": true` в разделе `"HostStats"`.
А чтобы еще считался и трафик (`"bytesRead"`, `"bytesSent"`), то нужно включить `TrafficStats: true` там же.

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

## Установка компонентов IIS

Требуется чтобы был установлен **.NET 8.0 Hosting Bundle**. Взять можно тут: https://dotnet.microsoft.com/en-us/download/dotnet/8.0.

Также нужно включить модуль **Application Initialization**. Он позволяет IIS автоматически прогревать сайт и запускать ASP.NET Core приложение сразу после старта пула приложений, без ожидания первого HTTP-запроса.
Это необходимо, чтобы фоновые службы стартовали сразу после перезапуска сервера или IIS.

**Как включить Application Initialization**:

1. Открыть **Server Manager**.
2. Выбрать пункт **Add Roles and Features**.
3. Дойди до раздела `Web Server (IIS) → Web Server → Application Development`.
4. Отметить чекбокс **Application Initialization**.
5. Завершить установку и при необходимости перезапусти IIS.

## Сборка и установка ProxyMapService

1. Собрать и опубликовать (Publish) приложение в bin\Release\net8.0\publish.
2. Создать каталог для приложения с именем, например, ProxyMapService в C:\inetpub\wwwroot.
3. Скопировать в него файлы из bin\Release\net8.0\publish.
4. Создать appsettings.Production.json и прописать в него конфигурацию (см. ниже раздел "Настройка").
5. Создать в IIS пул приложения с именем, например PortMapPool.
6. В диалоговом окне **Дополнительные параметры** пула PortMapPool выбрать:
    * **Режим запуска** = **Always Running**
    * **Тайм-аут простоя (в минутах)** = **0**.
7. В IIS преобразовать каталог C:\inetpub\wwwroot\PortMapService в приложение. При этом, указать для него ранее созданный **Пул приложений** PortMapPool.
8. В дополнительных параметрах приложения PortMapService выбрать:
    * **Предварительная установка включена** = **True**.

## Настройка

Настройка параметров осуществляется в файле appsettings.json (или в appsettings.Production.json в продакшене).

### JWT Authentication (для использования API)

Для использования API следует настроить раздел "Authentication" / "Jwt".
Сервис авторизации JWT можно использовать этот: https://github.com/optinsoft/YregAuthService (установить как отдельное приложение ASP.NET на тот же сервер, что и ProxyMapService).

Путь | Описание | Тип | Значение по-умолчанию |
-----| ---------|-----|-----------------------|
Authentication.Jwt.Enabled | Требуется аутентификация для доступа к API | bool | false |
Authentication.Jwt.Issuer | Издатель JWT-токена | string | "" (пустая строка) |
Authentication.Jwt.Audience | Для кого предназначен JWT-токен (URL) | string | "" (пустая строка) |
Authentication.Jwt.Key | Ключ для верификации JWT-токена | string | "" (пустая строка) |

### ProxyMappings

Открытые соединения, правила авторизации и соответствие прокси серверу задаются в разделе ProxyMappings.

Путь | Описание | Тип | Значение по-умолчанию |
-----| ---------|-----|-----------------------|
ProxyMappings[].Listen.Port | TCP порт | int | 5000 |
ProxyMappings[].Listen.RejectHttpProxy | Отклонять все HTTP (не CONNECT) соединения | bool | false |
ProxyMappings[].Authentication.Required | Проверять наличие HTTP заголовка Proxy-Authorization; если отсутствует, то возвращать ошибку 407 Proxy Authentication Required | bool | false |
ProxyMappings[].Authentication.Verify | Проверять HTTP заголовок Proxy-Authorization. Он должен содержать Basic b64, где b64 - это закодированная с помощью Base64 строка пользователь:пароль; пользователь и пароль задаются в параметрах (см. следующие два параметра) | bool | false |
ProxyMappings[].Authentication.Username | Имя пользователя | string | "" (пустая строка) |
ProxyMappings[].Authentication.Password | Пароль | string | "" (пустая строка) |
ProxyMappings[].Authentication.SetAuthentication | Добавлять (при наличии - заменять) HTTP заголовок Proxy-Authorization | bool | false |
ProxyMappings[].ProxyServers | Массив прокси серверов (см. описание `ProxyServer` ниже) | List<ProxyServer> | [] |

### ProxyServer

Путь | Описание | Тип | Значение по-умолчанию |
-----| ---------|-----|-----------------------|
Host | Хост прокси-сервера | string | Отсутствует (обязательное поле) |
Port | Порт прокси-сервера | int | Отсутствует (обязательное поле) |
ProxyType | Тип прокси-сервера. Возможные значения: Http, Socks4, Socks5 | string | Http |
Username | Имя пользователя для аутентификации прокси | string | "" (пустая строка) |
Password | Пароль для аутентификации прокси | string | "" (пустая строка) |

### HostRules

Правила для маршрутизации трафика задаются в разделе HostRules:

Путь | Описание | Тип | Пример |
-----| ---------|-----|--------|
HostRules[].Pattern | Регулярное выражение для имени хоста | String | "mozilla\\.(com\|org\|net)$" |
HostRules[].Action | Действие, которое нужно выполнить если имя хоста удовлетворяет выражению Pattern. Может принимать одно из трех значений: Allow (соединяться с хостом через прокси), Deny (отказывать в соединении), Bypass (соединяться с хостом напрямую, без прокси) | String | Deny |
HostRules[].OverrideHostName | Переопределить имя хоста, к которому осуществляется подключение. Опционально (null если не переопределять имя хоста) | String| "www.google.com" |
HostRules[].OverrideHostPort | Переопределить порт хоста, к которому осуществлятся подключение. Опционально (null если не переопределять порт хоста) | int | 81 |
HostRules[].ProxyServer | Использовать этот прокси-сервер когда `Action`=`Allow`. Опционально (null если использовать прокси-сервер из массива ProxyMappings[].ProxyServers) | ProxyServer | {"Host":"localhost", "Port":8888, "ProxyType":"Http"} |

Правила просматриваются в том же порядке, что указаны в HostRules. Если несколько правил применимы к хосту, то применяется последнее. То есть, например, можно запретить все соединения, кроме соединения с www.google.com:

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

## Благодарности

Код прокси сервера позаимствован (модифицирован и доработан) отсюда: https://github.com/agabani/PassThroughProxy
