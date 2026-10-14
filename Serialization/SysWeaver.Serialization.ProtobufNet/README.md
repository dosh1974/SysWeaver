# SysWeaver.Serialization.ProtobufNet

[⬆ SysWeaver overview](../../README.md)

> Serializer plug-in: **proto** (binary) backed by protobuf-net.

| | |
|---|---|
| **Layer** | Serialization |
| **Kind** | Plug-in (serializer, `ISerializerType`) |

## Purpose

Adds Protocol Buffers via protobuf-net. The API server lists `proto` among its default input/output formats, but the format is only actually available when this plug-in is registered.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Manifest["manifest entry or Register call"] --> SerM["SerManager<br/>registry by extension"]
  Plugin["ProtobufNet"] --> SerM
  SerM -->|best priority per extension| Users["API server, key/value stores,<br/>remote calls, exports"]
```

All data that SysWeaver moves — web API payloads, stored values, remote API calls, table exports — goes through the serializer registry in [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md). Adding a plug-in makes its format available everywhere at once; clients choose it through HTTP `Accept` / `Content-Type` headers.

## Key features

- Registered for `proto` (`application/x-protobuf`) with priority 0, the only bundled `proto` serializer.
- `ProtobufNetSerializer` (singleton `Instance`, binary only) needs **no** protobuf attributes: an internal builder adds each type to a private `RuntimeTypeModel` on first use, mapping all instance fields (public and non-public, including auto-property backing fields) and registering derived types as sub types.
- Constructors are not called when deserializing; `DateTime.Kind` is preserved.
- Registered through the manifest like any service, or with one static call in code.

## Key components

- [`ProtobufNetSerializer`](ProtobufNetSerializer.cs) — the serializer type
- [`SerializerBuilder`](SerializerBuilder.cs) — adds types to the runtime model without attributes

## Using it

The service manager recognises serializer types and registers them instead of creating an instance:

```json
{ "Type": "SysWeaver.Serialization.ProtobufNetSerializer, SysWeaver.Serialization.ProtobufNet" }
```

Or register it from code at startup: `SysWeaver.Serialization.ProtobufNetSerializer.Register();`

## Limitations and considerations

- Field and sub-type numbers are assigned automatically from reflection and discovery order, so the wire format is not a stable contract: don't use it for data that must be read by other languages, other builds or (for class hierarchies) other processes.
- `SerializerOptions` are ignored.
- Selection between serializers of the same extension is by priority; this is currently the only bundled `proto` implementation.

## Relationships

- **Project:** [`SysWeaver.Serialization.ProtobufNet.csproj`](SysWeaver.Serialization.ProtobufNet.csproj)
- **Builds on:** [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md)
- **Third-party:** [protobuf-net](https://www.nuget.org/packages/protobuf-net)
