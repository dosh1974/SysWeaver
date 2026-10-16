# Caches

[⬆ SysWeaver.IpLocation](../README.md)

> The IP location cache contract and the built-in in-memory cache.

| | |
|---|---|
| **Project** | [SysWeaver.IpLocation](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.IpLocation.Caches`) |

## Purpose

`IpLocationService` uses an `IIpLocationCache` registered before it in the manifest; when none is registered it creates an `IpLocationMemoryCache` with its own `MaxCachedMinutes`. A cache returns a cached location or calls the supplied function to fetch it from the sources.

## Contents

| Item | Description |
|---|---|
| [`IIpLocationCache.cs`](IIpLocationCache.cs) | The cache contract (`Get(ip, getFromSource)`) |
| [`IpLocationMemoryCache.cs`](IpLocationMemoryCache.cs) | In-memory cache |
| [`IpLocationMemoryCacheParams.cs`](IpLocationMemoryCacheParams.cs) | Its parameters (`MaxCachedMinutes`, default one day) |

## Relationships

- **Part of:** [SysWeaver.IpLocation](../README.md)
- **Implemented by:** [SysWeaver.IpLocation.MySqlCache](../../SysWeaver.IpLocation.MySqlCache/README.md) (persistent cache)
- **Related:** [Sources/](../Sources/README.md)
