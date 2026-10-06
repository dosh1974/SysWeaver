# SysWeaver.Serialization.NewtonsoftJson

[⬆ SysWeaver overview](../../README.md)

> Serializer plug-in: **json** (text) backed by Newtonsoft.Json.

| | |
|---|---|
| **Layer** | Serialization |
| **Kind** | Serializer plug-in (`ITextSerializerType`) |
| **Selection priority** | 1 (above System.Text.Json, below SysWeaver.Json and SafeJson) |

## Purpose

Wraps the widely used Newtonsoft.Json library, including a custom member resolver and a formatted-output helper. It is the *reading* half of [SysWeaver.Serialization.SafeJson](../../SysWeaver.Serialization.SafeJson/README.md) and is used by projects that need Newtonsoft features (e.g. Chart.js configuration objects).

## How it fits into SysWeaver

```mermaid
flowchart LR
  Manifest["manifest entry or Register call"] --> SerM["SerManager<br/>registry by extension"]
  Plugin["NewtonsoftJson"] --> SerM
  SerM -->|best priority per extension| Users["API server, key/value stores,<br/>remote calls, exports"]
```

All data that SysWeaver moves — web API payloads, stored values, remote API calls, table exports — goes through the serializer registry in [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md). Adding a plug-in makes its format available everywhere at once; clients choose it through HTTP `Accept` / `Content-Type` headers.

## Key features

- Tolerant, feature-rich JSON parsing.
- `NewtonsoftJsonSerializer` (singleton `Instance`) writes `$type` information where needed for `Compact`, on every object (indented) for `Verbose` and never for `Typeless`; reading honours `$type` (`TypeNameHandling.Auto`) and resolves names through `TypeNameResolver`.
- `MemberResolver` (public contract resolver, also used by Chart.js) serializes fields and skips read-only fields and get-only properties.
- `NewtonsoftJsonSerializer.ToFormattedJson` pretty-prints objects with byte arrays as aligned rows of numbers instead of base64.
- Deserializers are pooled, so concurrent use is cheap and safe.
- Registered through the manifest like any service, or with one static call in code.

## Limitations and considerations

- Slower and more allocation-heavy than the span-based serializers.
- **Security:** `$type` names in the input are resolved with `TypeFinder.GetForData`: only types allowed by the `DataTypePolicy` (SysWeaver and the application's own types, simple values, collections, plus assemblies marked with `[assembly: SerializableTypes]`) can be instantiated, known gadget types are always denied, and the type must be assignable to the declared type.
- `FromString(string)` throws `NullReferenceException` when the text deserializes to null but isn't the literal `null` (e.g. empty text), unlike the other overloads which return null.
- Selection between serializers of the same extension is by priority, so the effective JSON implementation depends on which plug-ins are registered (bundled priorities: SafeJson 10, SysWeaver.Json 2, Newtonsoft 1, System.Text.Json 0, CompactJson / Jil / SpanJson / Utf8Json -5).

## Using it

The service manager recognises serializer types and registers them instead of creating an instance:

```json
{ "Type": "SysWeaver.Serialization.NewtonsoftJsonSerializer, SysWeaver.Serialization.NewtonsoftJson" }
```

Or register it from code at startup: `SysWeaver.Serialization.NewtonsoftJsonSerializer.Register();`

## Relationships

- **Project:** [`SysWeaver.Serialization.NewtonsoftJson.csproj`](SysWeaver.Serialization.NewtonsoftJson.csproj)
- **Builds on:** [SysWeaver.Serialization](../../SysWeaver.Serialization/README.md)
- **Used by:** [SysWeaver.MicroService.ChartJs](../../SysWeaver.MicroService.ChartJs/README.md), [SysWeaver.Serialization.SafeJson](../../SysWeaver.Serialization.SafeJson/README.md)
