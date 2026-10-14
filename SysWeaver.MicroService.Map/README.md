# SysWeaver.MicroService.Map

[⬆ SysWeaver overview](../README.md)

> Web API and AI tools that generate SVG maps with SysWeaver.Map.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Micro service |

## Purpose

Expose [SysWeaver.Map](../SysWeaver.Map/README.md) to web pages and to LLM chats, so a page or an AI assistant can request a styled map of the world, a continent or a country with highlighted regions.

## Key features

- `GenerateMap` web API: returns the map as `image/svg+xml` for signed-in users, with client and request caching
- AI tools (`IHaveAiTools`): `GetMapRegions` lists the regions of a map, `BuildMap` generates a map and attaches it to the chat as an SVG file

## Key components

- [`MapService`](MapService.cs) — the service class

## Using it

```json
{ "Type": "SysWeaver.MicroService.MapService, SysWeaver.MicroService.Map" }
```

## Limitations and considerations

- `BuildMap` only works inside an AI tool call (it returns null without an AI tool context)

## Relationships

- **Project:** [`SysWeaver.MicroService.Map.csproj`](SysWeaver.MicroService.Map.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.Map](../SysWeaver.Map/README.md), [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md)
