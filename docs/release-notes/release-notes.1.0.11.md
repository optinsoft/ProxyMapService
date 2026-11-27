# ProxyMapService 1.0.11 Release Notes

## Fixes

* **Session API over SOCKS4/SOCKS5 was broken** (regression of 1.0.9): the SOCKS host action handlers (`Socks4HostActionHandler`, `Socks5HostActionHandler`) routed requests to the Session API host to the plain-HTTP step (`HandleStep.HttpSessionAPI`) instead of the SOCKS-specific ones (`Socks4SessionAPI` / `Socks5SessionAPI`). As a result the SOCKS success reply was never sent — a SOCKS client was still waiting for the connect grant while the Session API answered on the raw socket, so requests hung or failed. The handlers now dispatch to the correct protocol-specific session API handlers, which first send the SOCKS success reply and then hand the connection over to `SessionAPIHandler`.
