# SysWeaver.Serialization.SpanJson

[⬆ SysWeaver overview](../../README.md)

> Serializer plug-in: **json** (text) backed by SpanJson.

| | |
|---|---|
| **Layer** | Serialization |
| **Kind** | Serializer plug-in (`ISerializerType`) |
| **Selection priority** | low among serializers for `json` |

## Purpose

An alternative span-based JSON implementation registered with a low priority, mainly useful for comparison and compatibility scenarios.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Manifest["manifest entry or Register call"] --> SerM["SerManager<br/>registry by extension"]
  Plugin["SpanJson"] --> SerM
  SerM -->|best priority per extension| Users["API server, key/value stores,<br/>remote calls, exports"]
```

All data that SysWeaver moves — web API payloads, stored values, remote API calls, table exports — goes through the serializer registry in [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md). Adding a plug-in makes its format available everywhere at once; clients choose it through HTTP `Accept` / `Content-Type` headers.

## Key features

- Registered through the manifest like any service, or with one static call in code.

## Limitations and considerations

- Selection between serializers of the same extension is by priority, so the effective JSON implementation depends on which plug-ins are registered.

## Using it

The service manager recognises serializer types and registers them instead of creating an instance:

```json
{ "Type": "SysWeaver.Serialization.SpanJsonSerializer, SysWeaver.Serialization.SpanJson" }
```

Or register it from code at startup: `SysWeaver.Serialization.SpanJsonSerializer.Register();`

## Relationships

- **Project:** [`SysWeaver.Serialization.SpanJson.csproj`](SysWeaver.Serialization.SpanJson.csproj)
- **Builds on:** [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md)
