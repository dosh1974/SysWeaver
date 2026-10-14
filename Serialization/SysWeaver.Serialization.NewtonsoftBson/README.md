# SysWeaver.Serialization.NewtonsoftBson

[⬆ SysWeaver overview](../../README.md)

> Serializer plug-in: **bson** (binary) backed by Newtonsoft.Json.Bson.

| | |
|---|---|
| **Layer** | Serialization |
| **Kind** | Plug-in (serializer, `ISerializerType`) |

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

- Registered for `bson` (`application/bson`) with priority 1, the only bundled `bson` serializer.
- `NewtonsoftBsonSerializer` (singleton `Instance`, binary only) writes with an internal member resolver that skips read-only fields and get-only properties, and adds `$type` information (where needed for `Compact`, everywhere for `Verbose`, never for `Typeless`).
- Root values that BSON can't hold (`null`, numbers, strings, enums, dates, `Guid`, `byte[]` etc) are written wrapped as `{"$swRootValue": value}` and unwrapped when read; root arrays/collections are read back.
- Registered through the manifest like any service, or with one static call in code.

## Key components

- [`NewtonsoftBsonSerializer`](NewtonsoftBsonSerializer.cs) — the serializer type

## Using it

The service manager recognises serializer types and registers them instead of creating an instance:

```json
{ "Type": "SysWeaver.Serialization.NewtonsoftBsonSerializer, SysWeaver.Serialization.NewtonsoftBson" }
```

Or register it from code at startup: `SysWeaver.Serialization.NewtonsoftBsonSerializer.Register();`

## Limitations and considerations

- BSON is rarely needed outside MongoDB-style ecosystems.
- Other BSON readers see the `$swRootValue` wrapper document for wrapped root values.
- **Security:** reading honours `$type` (`TypeNameHandling.Auto`), resolving type names with `TypeNameResolver.GetForData`, so only types allowed by the `DataTypePolicy` (and assignable to the declared type) can be instantiated. Global `JsonConvert.DefaultSettings` are not applied.
- Selection between serializers of the same extension is by priority; this is currently the only bundled `bson` implementation.

## Relationships

- **Project:** [`SysWeaver.Serialization.NewtonsoftBson.csproj`](SysWeaver.Serialization.NewtonsoftBson.csproj)
- **Builds on:** [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md)
- **Third-party:** [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json), [Newtonsoft.Json.Bson](https://www.nuget.org/packages/Newtonsoft.Json.Bson)
