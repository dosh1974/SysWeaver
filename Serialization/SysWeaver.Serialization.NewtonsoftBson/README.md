# SysWeaver.Serialization.NewtonsoftBson

[⬆ SysWeaver overview](../../README.md)

> Serializer plug-in: **bson** (binary) backed by Newtonsoft.Json.Bson.

| | |
|---|---|
| **Layer** | Serialization |
| **Kind** | Serializer plug-in (`ISerializerType`) |
| **Selection priority** | 1 (the only bundled serializer for `bson`) |

## Purpose

Adds BSON (binary JSON) support. The API server lists `bson` among its default input/output formats, but the format is only actually available when this plug-in is registered.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Manifest["manifest entry or Register call"] --> SerM["SerManager<br/>registry by extension"]
  Plugin["NewtonsoftBson"] --> SerM
  SerM -->|best priority per extension| Users["API server, key/value stores,<br/>remote calls, exports"]
```

All data that SysWeaver moves — web API payloads, stored values, remote API calls, table exports — goes through the serializer registry in [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md). Adding a plug-in makes its format available everywhere at once; clients choose it through HTTP `Accept` / `Content-Type` headers.

## Key features

- Binary JSON for clients that prefer it.
- `NewtonsoftBsonSerializer` (singleton `Instance`, binary only) writes with an internal `MemberResolver` that skips read-only fields and get-only properties, and adds `$type` information (where needed for `Compact`, everywhere for `Verbose`, never for `Typeless`).
- Registered through the manifest like any service, or with one static call in code.

## Limitations and considerations

- BSON is rarely needed outside MongoDB-style ecosystems.
- Reading uses Newtonsoft's default serializer settings (no type name handling), so polymorphic members written with `$type` are not restored.
- BSON needs an object or array at the root: primitive root values can't be serialized, and root arrays/collections can't be read back (the reader does not enable `ReadRootValueAsArray`).
- Selection between serializers of the same extension is by priority; this is currently the only bundled `bson` implementation.

## Using it

The service manager recognises serializer types and registers them instead of creating an instance:

```json
{ "Type": "SysWeaver.Serialization.NewtonsoftBsonSerializer, SysWeaver.Serialization.NewtonsoftBson" }
```

Or register it from code at startup: `SysWeaver.Serialization.NewtonsoftBsonSerializer.Register();`

## Relationships

- **Project:** [`SysWeaver.Serialization.NewtonsoftBson.csproj`](SysWeaver.Serialization.NewtonsoftBson.csproj)
- **Builds on:** [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md)
