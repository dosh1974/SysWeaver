# SysWeaver.Serialization

[⬆ SysWeaver overview](../README.md)

> The serialization abstraction of SysWeaver and its registry: every component serializes through named, prioritised serializer types selected by file extension. Includes System.Text.Json and XML implementations.

| | |
|---|---|
| **Layer** | Serialization |
| **Kind** | Library |

## Purpose

Decouple *what* is serialized from *how*. Services, the API server, storage and remote connections ask the registry for "json", "xml", "msgpack", … and get the best registered implementation. Formats are added by dropping in plug-ins, without touching the consumers.

## How it fits into SysWeaver

```mermaid
flowchart TB
  subgraph Registry["SerManager"]
    direction LR
    J["json"]
    X["xml"]
    B["bson / proto / msgpack / swbin"]
  end
  Builtin["System.Text.Json and XML<br/>built in"] --> Registry
  Plugins["plug-ins in Serialization/ folder"] --> Registry
  Registry --> Api["Web API<br/>Accept / Content-Type negotiation"]
  Registry --> Store["Key/value stores, DB blobs"]
  Registry --> Remote["Remote API calls"]
  Registry --> Export["Table exports"]
```

## Key features

- One process-wide registry (`SerManager`); formats are addressed by the same short names as file extensions, with or without a leading dot (`"json"`, `".json"`).
- A serializer type (`ISerializerType`) is a stateless singleton with a name, extension, MIME type, Content-Type header, encoding (null for binary) and a *priority*; `Serialize<T>` returns `ReadOnlyMemory<byte>`, `Create<T>` reads from `ReadOnlyMemory<byte>` / `ReadOnlySpan<byte>`.
- When several serializers share an extension, the highest priority wins (on a tie, the last registered) — this is how a plug-in becomes the default JSON implementation.
- Text serializers (`ITextSerializerType`) additionally support strings (`ToString<T>`, `FromString<T>`); `SerManager.GetText` only considers these.
- `SerializerOptions` (`Compact`, `Verbose`, `Typeless`) are hints: Newtonsoft-based serializers use them for indentation and `$type` output, System.Text.Json and CompactJson only for indentation, the rest ignore them.
- Built in: `NetJsonSerializer` (System.Text.Json, priority 0) is always registered; `NetXmlSerializer` (XmlSerializer) is available via `NetXmlSerializer.Register()`.
- `SerExtensions` adds `ToJsonString`, `ToJsonData` and `FromJsonData` extension methods that use the active `json` serializer (default option: `Verbose`).
- `SerTools.SerializeWithoutType` / `TextSerTools.ToStringWithoutType` serialize an `object` as its runtime type; `SerTools.MakeHeader` builds `Content-Type` values with a charset.
- `TypeNameResolver` resolves type names through `TypeFinder` (SysWeaver.Common); `GetForData` is used for names from serialized data and only returns types allowed by the `DataTypePolicy`.
- Plug-ins for Newtonsoft JSON/BSON, Protobuf, MessagePack, SysWeaver's own JSON and binary formats, and several alternative JSON libraries.

## Key components

- [`SerManager`](SerManager.cs) — the process-wide serializer registry
- [`ISerializerType`](ISerializerType.cs) — the serializer contract (and `ITextSerializerType`)
- [`SerializerOptions`](SerializerOptions.cs) — output style hints
- [`NetJsonSerializer`](NetJsonSerializer.cs) — the always registered System.Text.Json serializer
- [`SerExtensions`](SerExtensions.cs) — JSON convenience extension methods

## Using it

```csharp
using SysWeaver.Serialization;
var json = SerManager.GetText("json");
string text = json.ToString(new { Id = 1 });
```

Register plug-ins at startup (e.g. `SysWeaverJsonSerializer.Register();`) or in the manifest; the service manager calls the static `Register` method of any `ISerializerType` listed, e.g. `{ "Type": "SysWeaver.Serialization.SysWeaverJsonSerializer, SysWeaver.Serialization.SwJson" }`.

## Limitations and considerations

- The registry is process-wide static state: registering a higher-priority serializer changes behaviour for every consumer in the process. Register serializers at startup: `SerManager.AddType` is not thread-safe, and many consumers resolve and cache a serializer on first use, so later registrations are not seen by them (`SerExtensions` resolves the serializer on every call).
- The API server's default formats include `xml`, `proto` and `bson`, but these are only available when their serializer is registered (`NetXmlSerializer.Register()` or the plug-ins).
- `NetJsonSerializer` writes with relaxed escaping (HTML-sensitive and non-ASCII characters are not escaped); escape before embedding output in HTML.
- Extension lookups are case-sensitive; pass lowercase extensions.
- Behaviour differences between JSON implementations (casing, field handling, polymorphism) mean the choice of default JSON serializer is an application-level decision.

## Relationships

- **Project:** [`SysWeaver.Serialization.csproj`](SysWeaver.Serialization.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.DbSimpleStack](../SysWeaver.DbSimpleStack/README.md), [SysWeaver.ExchangeRate](../SysWeaver.ExchangeRate/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.Knowledge](../SysWeaver.Knowledge/README.md), [SysWeaver.Map](../SysWeaver.Map/README.md), [SysWeaver.Remote.Connection](../SysWeaver.Remote.Connection/README.md), [SysWeaver.Serialization.CompactJson](../Serialization/SysWeaver.Serialization.CompactJson/README.md), [SysWeaver.Serialization.MessagePack](../Serialization/SysWeaver.Serialization.MessagePack/README.md), [SysWeaver.Serialization.NewtonsoftBson](../Serialization/SysWeaver.Serialization.NewtonsoftBson/README.md), [SysWeaver.Serialization.NewtonsoftJson](../Serialization/SysWeaver.Serialization.NewtonsoftJson/README.md), [SysWeaver.Serialization.ProtobufNet](../Serialization/SysWeaver.Serialization.ProtobufNet/README.md), [SysWeaver.Serialization.SafeJson](../Serialization/SysWeaver.Serialization.SafeJson/README.md), [SysWeaver.Serialization.SpanJson](../Serialization/SysWeaver.Serialization.SpanJson/README.md), [SysWeaver.Serialization.SwBinary](../Serialization/SysWeaver.Serialization.SwBinary/README.md), [SysWeaver.Serialization.SwJson](../Serialization/SysWeaver.Serialization.SwJson/README.md), [SysWeaver.Serialization.Utf8Json](../Serialization/SysWeaver.Serialization.Utf8Json/README.md), [SysWeaver.Storage](../SysWeaver.Storage/README.md), [SysWeaver.TableData](../SysWeaver.TableData/README.md); in SysWeaver.Tools: [Compress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Compress/README.md), [SvgOpt](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SvgOpt/README.md)
