# SysWeaver.MicroService.AspHttpServer

[⬆ SysWeaver overview](../README.md)

> Micro service that runs the Kestrel-based SysWeaver web server.

| | |
|---|---|
| **Layer** | Web platform |
| **Kind** | Micro service |

## Purpose

The Kestrel counterpart of [SysWeaver.MicroService.NetHttpServer](../SysWeaver.MicroService.NetHttpServer/README.md): `AspHttpServerService` creates an [SysWeaver.AspHttpServer](../SysWeaver.AspHttpServer/README.md) and integrates it with the service manager in the same way (through `HttpServerServiceBase` in [SysWeaver.MicroService.HttpServer](../SysWeaver.MicroService.HttpServer/README.md)).

## Key components

- [`AspHttpServerService`](AspHttpServerService.cs) — the micro service
- [`AspHttpServerServiceParams`](AspHttpServerServiceParams.cs) — the server parameters plus `Start`, `InstanceName`, `TranslatorInstance` and `AuditInstance`

## Using it

```json
{ "Type": "SysWeaver.MicroService.AspHttpServerService, SysWeaver.MicroService.AspHttpServer",
  "Params": { "ListenOn": [ { "Prefix": "https://*:443", "Certificate": "*" } ], "EnableHttp3": true } }
```

`"Certificate": "*"` uses the provider named after the prefix host, else the first registered certificate provider.

## Limitations and considerations

- Certificate providers, the auth manager, the firewall handler, the translator and the audit service are looked up once at construction and must be registered before this service.
- Use one server service per process.
- Prefixes must not contain paths or host names (Kestrel limitation, see [SysWeaver.AspHttpServer](../SysWeaver.AspHttpServer/README.md)).

## Relationships

- **Project:** [`SysWeaver.MicroService.AspHttpServer.csproj`](SysWeaver.MicroService.AspHttpServer.csproj)
- **Builds on:** [SysWeaver.AspHttpServer](../SysWeaver.AspHttpServer/README.md), [SysWeaver.Auth.Core](../SysWeaver.Auth.Core/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.MicroService.HttpServer](../SysWeaver.MicroService.HttpServer/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
- **Used by:** [SwServerManager](https://github.com/dosh1974/SysWeaver.Services/blob/main/SwServerManager/README.md)
