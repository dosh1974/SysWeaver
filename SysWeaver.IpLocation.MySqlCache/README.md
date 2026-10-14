# SysWeaver.IpLocation.MySqlCache

[⬆ SysWeaver overview](../README.md)

> Persistent MySQL cache for IP location lookups.

| | |
|---|---|
| **Layer** | Storage / Data |
| **Kind** | Plug-in (`IIpLocationCache`) |

## Purpose

Keep IP lookups across restarts and share them between instances, reducing calls (and cost) to external geolocation sources used by [SysWeaver.IpLocation](../SysWeaver.IpLocation/README.md).

## How it fits into SysWeaver

Registered before `IpLocationService` in the manifest, it replaces the service's built-in memory cache. Lookups go through a short-lived memory cache, then the MySQL table, and only then to the IP location sources; the result is stored in the table.

## Key features

- Two levels: in-memory (`MaxMemCachedMinutes`, default 60) in front of a MySQL table (`DbCachedDays`, default 90)
- Table created automatically, in the `IpLocation` schema by default
- Unknown addresses (no location) are cached too, avoiding repeated paid lookups
- A *Locations* table in the web UI (requires the `Debug` role)

## Key components

- [`IpLocationMySqlCache`](IpLocationMySqlCache.cs) - the `IIpLocationCache` implementation
- [`IpLocationMySqlCacheParams`](IpLocationMySqlCacheParams.cs) - database connection and cache durations
- [`DbIpLocation`](DbIpLocation.cs) - the table row

## Using it

```json
{ "Type": "SysWeaver.IpLocation.Caches.IpLocationMySqlCache, SysWeaver.IpLocation.MySqlCache",
  "Params": { "CredFile": "$(KeyFolder)/MySql.txt" } }
```

## Limitations and considerations

- Requires a MySQL database ([SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md)); the cached data ages like any geolocation data.

## Relationships

- **Project:** [`SysWeaver.IpLocation.MySqlCache.csproj`](SysWeaver.IpLocation.MySqlCache.csproj)
- **Builds on:** [SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md), [SysWeaver.IpLocation](../SysWeaver.IpLocation/README.md)
- **Used by:** [IpLocationService](https://github.com/dosh1974/SysWeaver.Services/blob/main/IpLocationService/README.md) (SysWeaver.Services)
