# SysWeaver.Serialization.SwJson

[⬆ SysWeaver overview](../../README.md)

> Serializer plug-in: **json** (text) backed by SysWeaver's own JSON reader/writer.

| | |
|---|---|
| **Layer** | Serialization |
| **Kind** | Serializer plug-in (`ITextSerializerType`) |
| **Selection priority** | 2 (above Newtonsoft 1 and System.Text.Json 0; below SafeJson 10) |

## Purpose

The framework's own, dependency-free JSON implementation. It has the highest priority of the plain JSON serializers, so registering it makes it the default JSON implementation (for example for web API responses). It is also the *writing* half of [SysWeaver.Serialization.SafeJson](../../SysWeaver.Serialization.SafeJson/README.md).

## How it fits into SysWeaver

```mermaid
flowchart LR
  Manifest["manifest entry or Register call"] --> SerM["SerManager<br/>registry by extension"]
  Plugin["SwJson"] --> SerM
  SerM -->|best priority per extension| Users["API server, key/value stores,<br/>remote calls, exports"]
```

All data that SysWeaver moves — web API payloads, stored values, remote API calls, table exports — goes through the serializer registry in [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md). Adding a plug-in makes its format available everywhere at once; clients choose it through HTTP `Accept` / `Content-Type` headers.

## Key features

- No external dependency.
- Preferred over the other plain JSON serializers when registered.
- `SysWeaverJsonSerializer` (singleton `Instance`) wraps the two engines:
  - `JsonWriter.ToJsonString<T>`, `JsonWriter.ToJsonBytes<T>` (optionally into a caller supplied buffer) write UTF-8 through pooled buffers (`BufferWriter`), so the only allocation is the result.
  - `JsonReader.Create<T>` / `JsonReader.Create(Type, ...)` parse UTF-8 bytes (or a string) directly.
- Per-type readers and writers are generated with compiled expression trees and cached for the life of the process: the first use of a type is slow, later calls are fast.
- Supported types: primitives, `decimal`, `string`, `char`, enums (written as numbers, read as names, numbers or flag lists), `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `byte[]` (base64), `Nullable<T>`, arrays, `ICollection<T>`, `IDictionary<K,V>` with string / number / bool / char / date / `Guid` keys, and classes / structs (public read/write properties and non-readonly public fields).
- Polymorphism: when the runtime type differs from the declared type, values are written Newtonsoft-style as `{"$type":"Name,Assembly",...}` (with `$value` / `$values` for boxed primitives and collections) and the reader re-creates that type. Type names can be customised with `JsonWriter.ToTypename`, `AssemblyMap` and `NamespaceMap` (set before first use).
- Culture-invariant number and date handling: shortest round-trip floats, ISO-8601 (`"o"`) dates, `"c"` time spans; only control characters, `"` and `\` are escaped.
- Lenient reader: unquoted keys, quoted numbers/booleans, `//` and `/* */` comments and trailing commas are accepted; unknown scalar members are skipped.
- Registered through the manifest like any service, or with one static call in code.

## Limitations and considerations

- As a custom implementation, edge cases of exotic types may behave differently from System.Text.Json or Newtonsoft; test your payload types.
- `SerializerOptions` are ignored: output is always compact (no indentation).
- Member names are matched case-sensitively; unknown members holding objects or arrays cause an error instead of being skipped.
- `NaN` / `Infinity` are written as bare tokens, which the SysWeaver reader accepts but strict JSON parsers reject.
- Integer values are not range-checked when reading (out-of-range values wrap silently).
- `$type` in the input can create any loaded type; only read data from trusted sources into `object` / base class members.
- Selection between serializers of the same extension is by priority, so the effective JSON implementation depends on which plug-ins are registered.

## Using it

The service manager recognises serializer types and registers them instead of creating an instance:

```json
{ "Type": "SysWeaver.Serialization.SysWeaverJsonSerializer, SysWeaver.Serialization.SwJson" }
```

Or register it from code at startup: `SysWeaver.Serialization.SysWeaverJsonSerializer.Register();`

## Relationships

- **Project:** [`SysWeaver.Serialization.SwJson.csproj`](SysWeaver.Serialization.SwJson.csproj)
- **Builds on:** [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md)
- **Used by:** [SysWeaver.Serialization.SafeJson](../../SysWeaver.Serialization.SafeJson/README.md)
