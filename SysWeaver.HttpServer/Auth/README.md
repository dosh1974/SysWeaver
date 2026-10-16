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
| [`NoSessionException.cs`](NoSessionException.cs) | Four exceptions with fixed messages: `NoSessionException` (the request has no `HttpSession`), `NoUserLoggedInException` (no logged in user), `UserAlreadyLoggedInException` (e.g. login or sign up while logged in) and `UserNotAllowedException` (the user or anonymous session lacks the required tokens) |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../README.md)
- **Used by:** [`FileProxy.cs`](../FileProxy.cs), and services such as [SysWeaver.AI](../../SysWeaver.AI/README.md), [SysWeaver.Chat](../../SysWeaver.Chat/README.md), [SysWeaver.MicroService.ChartJs](../../SysWeaver.MicroService.ChartJs/README.md), [SysWeaver.MicroService.UserManager](../../SysWeaver.MicroService.UserManager/README.md) and [SysWeaver.MicroService.UserStorage](../../SysWeaver.MicroService.UserStorage/README.md)
