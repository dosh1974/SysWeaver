# SysWeaver.TextMessage.Fake

[⬆ SysWeaver overview](../README.md)

> A fake SMS provider for development and testing: "sent" messages are kept in memory and incoming messages can be simulated.

| | |
|---|---|
| **Layer** | Users (test double) |
| **Kind** | Micro service |

## Purpose

Exercise phone verification and other SMS flows without a real SMS gateway.

## Limitations and considerations

- Development only: its endpoints declare no authentication of their own, so on a server with anonymous API defaults message contents would be public.
- Nothing is actually delivered.

## Relationships

- **Project:** [`SysWeaver.TextMessage.Fake.csproj`](SysWeaver.TextMessage.Fake.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.TableData](../SysWeaver.TableData/README.md)
