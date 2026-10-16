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
| [`HttpResponseException.cs`](HttpResponseException.cs) | Exception with an http status code, a message and the message language |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md) and services that throw it (e.g. [SysWeaver.ReverseProxy](../../SysWeaver.ReverseProxy/README.md), [SysWeaver.Chat.MySql](../../SysWeaver.Chat.MySql/README.md))
