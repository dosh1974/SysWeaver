# Sources

[⬆ SysWeaver.IpLocation](../README.md)

> The IP location source contract and the included sources: two commercial web APIs and another SysWeaver server.

| | |
|---|---|
| **Project** | [SysWeaver.IpLocation](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.IpLocation.Sources`) |

## Purpose

Sources are registered in the manifest before the `IpLocationService`, which tries them in registration order. The commercial sources call the provider's web API with an API key; `IlsSysWeaverService` calls `Api/IpLocation/GetLocation` on another server running the service, so one central server with API keys and a persistent cache can serve many.

## Contents

| Item | Description |
|---|---|
| [`IIpLocationSource.cs`](IIpLocationSource.cs) | The source contract (`LookUp(ip)`) |
| [`Ip2locationIoSource.cs`](Ip2locationIoSource.cs) | Source using [ip2location.io](https://www.ip2location.io/) |
| [`AppIpgeolocationIoSource.cs`](AppIpgeolocationIoSource.cs) | Source using [ipgeolocation.io](https://ipgeolocation.io/) |
| [`IlsSysWeaverService.cs`](IlsSysWeaverService.cs) | Source using another SysWeaver server |
| [`IlsSysWeaverServiceParams.cs`](IlsSysWeaverServiceParams.cs) | Its parameters: the remote connection (`BaseUrl`, credentials) |
| [`IRemote.cs`](IRemote.cs) | The remote interface of the `GetLocation` endpoint used by `IlsSysWeaverService` |

## Relationships

- **Part of:** [SysWeaver.IpLocation](../README.md)
- **Uses:** [SysWeaver.Remote.Connection](../../SysWeaver.Remote.Connection/README.md) (`IlsSysWeaverService`)
- **Related:** [Caches/](../Caches/README.md)
