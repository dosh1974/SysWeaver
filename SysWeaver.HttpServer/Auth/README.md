# Auth

[⬆ SysWeaver.HttpServer](../README.md)

> Exceptions that web API methods throw when a request's session or login state doesn't allow the operation.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../README.md) |
| **Kind** | Source code |

## Purpose

Shared exception types for session and authorisation failures, so that services report these cases in a uniform way. Authentication itself (sessions, tokens, login redirects) is implemented in the project root, e.g. [`HttpSession`](../HttpSession.cs) and [`HttpServerBase`](../HttpServerBase.cs).

## Contents

| Item | Description |
|---|---|
| [`NoSessionException.cs`](NoSessionException.cs) | Eight exceptions: `NoSessionException` (the request has no `HttpSession`), `NoUserLoggedInException` (no signed in user; thrown while the server resolves a handler it starts the normal sign in flow, thrown by a handler it answers 401), `UserNotAllowedException` (the user or anonymous session lacks the required tokens, answers 403), and the refusals that implement `IHttpRefusal` (SysWeaver.Common), which answer their status with the message as the reason instead of a 500: `UserAlreadyLoggedInException` (409, ex: sign in or sign up while signed in), `UserAlreadyExistException` (409, the account name is taken), `UserDoNotExistException` (404), `UserNoLongerExistException` (410, a known user was removed) and `AuthenticationFailedException` (400, the password could not be verified) |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../README.md)
- **Used by:** [`HttpServerBase.cs`](../HttpServerBase.cs) and [`HttpProxy.cs`](../HttpProxy.cs), and services such as [SysWeaver.AI](../../SysWeaver.AI/README.md), [SysWeaver.Chat](../../SysWeaver.Chat/README.md), [SysWeaver.MicroService.ChartJs](../../SysWeaver.MicroService.ChartJs/README.md), [SysWeaver.MicroService.PassKey](../../SysWeaver.MicroService.PassKey/README.md), [SysWeaver.MicroService.UserManager](../../SysWeaver.MicroService.UserManager/README.md) and [SysWeaver.MicroService.UserStorage](../../SysWeaver.MicroService.UserStorage/README.md)
