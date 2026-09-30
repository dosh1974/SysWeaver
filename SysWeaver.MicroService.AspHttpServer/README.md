# SysWeaver.MicroService.AspHttpServer

[⬆ SysWeaver overview](../README.md)

> Micro service that runs the Kestrel-based SysWeaver web server.

| | |
|---|---|
| **Layer** | HTTP (service integration) |
| **Kind** | Micro service |

## Purpose

The Kestrel counterpart of [SysWeaver.MicroService.NetHttpServer](../SysWeaver.MicroService.NetHttpServer/README.md); identical integration behaviour, different transport ([SysWeaver.AspHttpServer](../SysWeaver.AspHttpServer/README.md)).

## Limitations and considerations

- Use one server service per process.
- Prefixes must not contain paths (Kestrel limitation).

## Using it

```json
{ "Type": "SysWeaver.MicroService.AspHttpServerService, SysWeaver.MicroService.AspHttpServer",
  "Params": { "ListenOn": [ { "Prefix": "https://*:443", "Certificate": "*" } ] } }
```

## Relationships

- **Project:** [`SysWeaver.MicroService.AspHttpServer.csproj`](SysWeaver.MicroService.AspHttpServer.csproj)
- **Builds on:** [SysWeaver.AspHttpServer](../SysWeaver.AspHttpServer/README.md), [SysWeaver.Auth.Core](../SysWeaver.Auth.Core/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.MicroService.HttpServer](../SysWeaver.MicroService.HttpServer/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
