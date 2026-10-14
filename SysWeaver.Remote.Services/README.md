# SysWeaver.Remote.Services

[⬆ SysWeaver overview](../README.md)

> Ready-made remote API definitions for third-party services — currently the ip-api.com geolocation API.

| | |
|---|---|
| **Layer** | Networking |
| **Kind** | Library (remote API contracts) |

## Purpose

Show and provide the pattern for wrapping an external HTTP API as a SysWeaver remote interface, and supply a free IP geolocation lookup that can be registered as a service.

## How it fits into SysWeaver

The interfaces only declare the HTTP contract using [SysWeaver.Remote](../SysWeaver.Remote/README.md) attributes; listing one in a manifest with connection parameters makes the service manager create a client through [SysWeaver.Remote.Connection](../SysWeaver.Remote.Connection/README.md), which other services can then look up like any local service.

## Key features

- `IIpApiCom.GetIpInfo(address)` — GET `json/{ip}` on ip-api.com, returning a typed `IpApiResponse` (location, time zone, currency, ISP, proxy/mobile/hosting flags)
- JSON serialization and a 15 second response cache declared on the interface

## Key components

- [`IIpApiCom` / `IpApiResponse`](IpApiCom.cs) — the ip-api.com contract and response model

## Using it

```json
{ "Type": "SysWeaver.Remote.Services.IIpApiCom, SysWeaver.Remote.Services",
  "Params": { "BaseUrl": "http://ip-api.com/" } }
```

## Limitations and considerations

- Subject to the third-party service's availability, rate limits and terms of use (the free ip-api.com endpoint is HTTP only).
- `BaseUrl` must be given in the manifest; the interface has no built-in default.

## Relationships

- **Project:** [`SysWeaver.Remote.Services.csproj`](SysWeaver.Remote.Services.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md), [SysWeaver.Remote](../SysWeaver.Remote/README.md)
