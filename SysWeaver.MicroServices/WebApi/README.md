# WebApi

[⬆ SysWeaver.MicroServices](../README.md)

> The attributes that publish service methods as web APIs and set their URL, authorisation, caching, compression, rate limits and auditing.

| | |
|---|---|
| **Project** | [SysWeaver.MicroServices](../README.md) |
| **Kind** | Source code (attributes) |

## Purpose

These attributes are metadata only. [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md) reads them when it turns a service's methods into endpoints. See the project README for an example. All types are in the `SysWeaver.MicroService` namespace.

## Contents

| Item | Description |
|---|---|
| [`WebApiAttribute.cs`](WebApiAttribute.cs) | `[WebApi]` (publish a method), plus `[WebApiRaw]` / `[WebApiRawText]` (pre-serialised responses), `[WebApiServiceRateLimit]` / `[WebApiSessionRateLimit]`, `[WebApiAudit]` and `[WebApiAuditFilterParams]` / `[WebApiAuditFilterReturn]` |
| [`WebApiUrlAttribute.cs`](WebApiUrlAttribute.cs) | `[WebApiUrl]`: base URL (prefix) of the methods of a type |
| [`WebApiAuthAttribute.cs`](WebApiAuthAttribute.cs) | `[WebApiAuth]`: authorisation required for a type or method |
| [`IRunTimeWebApiAuth.cs`](IRunTimeWebApiAuth.cs) | Interface for services whose per-method auth is configured at run time (`MethodAuths`, `"*"` for all other methods) |
| [`WebApiClientCacheAttribute.cs`](WebApiClientCacheAttribute.cs) | `[WebApiClientCache]`: client side cache duration |
| [`WebApiRequestCacheAttribute.cs`](WebApiRequestCacheAttribute.cs) | `[WebApiRequestCache]` (server side cache, `WebApiCaches`: auto-detect, global or per session), the `...Static` variants and `WebApiTools` cache constants |
| [`WebApiCompressionAttribute.cs`](WebApiCompressionAttribute.cs) | `[WebApiCompression]`: preferred response compression, with `WebApiCompress` presets (`Best`, `Balanced`, `Fast`) |
| [`WebApiOptionalAttribute.cs`](WebApiOptionalAttribute.cs) | `[WebApiOptional]`: publish the API only when a member on the instance says it is available |

## Relationships

- **Part of:** [SysWeaver.MicroServices](../README.md)
- **Interpreted by:** [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md)
- **Related:** [`WebMenu/`](../WebMenu/README.md) (menu attributes often placed on the same methods)
