# SysWeaver.IpLocation.MySqlCache

[⬆ SysWeaver overview](../README.md)

> Persistent MySQL cache for IP location lookups.

| | |
|---|---|
| **Layer** | Networking / Data |
| **Kind** | Cache plug-in (`IIpLocationCache`) |

## Purpose

Keep IP lookups across restarts and share them between instances, reducing calls (and cost) to external geolocation sources used by [SysWeaver.IpLocation](../SysWeaver.IpLocation/README.md).

## Limitations and considerations

- Requires a MySQL database ([SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md)); the cached data ages like any geolocation data.

## Relationships

- **Project:** [`SysWeaver.IpLocation.MySqlCache.csproj`](SysWeaver.IpLocation.MySqlCache.csproj)
- **Builds on:** [SysWeaver.DbSimpleStack.MySql](../SysWeaver.DbSimpleStack.MySql/README.md), [SysWeaver.IpLocation](../SysWeaver.IpLocation/README.md)
