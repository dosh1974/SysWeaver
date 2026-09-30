# SysWeaver.Remote.Services

[⬆ SysWeaver overview](../README.md)

> Ready-made remote API definitions for third-party services — currently the ip-api.com geolocation API.

| | |
|---|---|
| **Layer** | Remote APIs |
| **Kind** | Remote API contracts |

## Purpose

Show and provide the pattern for wrapping an external HTTP API as a SysWeaver remote interface, and supply a free IP geolocation lookup that can be registered as a service.

## Key features

- Typed response model with documented fields.
- Response caching and JSON serialization declared on the interface.

## Limitations and considerations

- Subject to the third-party service's availability, rate limits and terms of use.

## Using it

See [SysWeaver.Remote.Connection](../SysWeaver.Remote.Connection/README.md) for the manifest entry that turns the interface into a registered service.

## Relationships

- **Project:** [`SysWeaver.Remote.Services.csproj`](SysWeaver.Remote.Services.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md), [SysWeaver.Remote](../SysWeaver.Remote/README.md)
