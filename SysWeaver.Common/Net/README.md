# Net

[⬆ SysWeaver.Common](../README.md)

> An exception type that lets a request handler respond with a specific http status code, and the network connections of the computer.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.Net`) |

## Purpose

Throwing `HttpResponseException` from an API method or request handler makes the http server respond with the given status code and message instead of a generic 500 error. The message can carry its language so that it can be translated.

Most network helpers (`NetworkTools`, `WebTools`) live in the project root, not in this folder.

## Contents

| Item | Description |
|---|---|
| [`HttpResponseException.cs`](HttpResponseException.cs) | Exception with an http status code, a message and the message language; **`IHttpRefusal`**: implement it (`int ResponseCode`, a 4xx) on an exception type that means "refused" (a user mistake or a changed state, ex: a link that no longer works, an address in use) rather than "the server failed": the http server answers that status with the exception message as the reason (`X-Sw-Reason: 1`, translated from english), not a 500, and it is no server problem. Use it when code also catches the type (ex: `catch (TokenExpiredException)`). **`HttpOutcomes`** + `HttpResponseException.Outcome` / `IHttpRefusal.Outcome`: the outcome kind the server sends as `X-Sw-Outcome` (`AlreadyDone`, `Refused`, `Gone`, `Denied`; `HttpOutcomes.Default(status, denied)` when not given); `HttpResponseException.AlreadyDone(msg)` / `Refused(msg)` / `Gone(msg)` (409 by default). |
| [`NetConnection.cs`](NetConnection.cs) | The network connections of this computer (like `netstat`): **`NetConnection`** (protocol, local and remote end point, state, owning process id, creation time), **`INetConnections`** (implemented by the Windows and Linux platform tools), **`NetConnections.Get(includeUdp)`** (falls back to the .NET network information without process ids on other platforms), `NetConnections.IsLocalOrLan` (loopback, private, link local, CGNAT, multicast and the IPv6 equivalents; IPv4 mapped addresses are normalized) and the state mapping (`FromMibState`, `FromLinuxState`). Used by [SysWeaver.MicroService.ServerManager](../../SysWeaver.MicroService.ServerManager/README.md). |
| [`IIpLocation.cs`](IIpLocation.cs) | A service that finds the estimated location of an ip address (ex: `SysWeaver.IpLocation.IpLocationService`); other services find it with `ServiceManager.TryGet<IIpLocation>()` without referencing the implementation |
| [`Location.cs`](Location.cs) | The estimated location of an ip address: ISO country, latitude / longitude, address, region code, city, autonomous system and the source of the information |
| [`IMcpService.cs`](IMcpService.cs) | Interface of the MCP service: register all `[WebApi]` methods of an object as MCP tools (and unregister them again) |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md) and services that throw it (e.g. [SysWeaver.ReverseProxy](../../SysWeaver.ReverseProxy/README.md), [SysWeaver.Chat.MySql](../../SysWeaver.Chat.MySql/README.md))
