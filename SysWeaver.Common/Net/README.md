# Net

[⬆ SysWeaver.Common](../README.md)

> An exception type that lets a request handler respond with a specific http status code.

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

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md) and services that throw it (e.g. [SysWeaver.ReverseProxy](../../SysWeaver.ReverseProxy/README.md), [SysWeaver.Chat.MySql](../../SysWeaver.Chat.MySql/README.md))
