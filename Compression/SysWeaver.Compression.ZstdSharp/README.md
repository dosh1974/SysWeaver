# SysWeaver.Compression.ZstdSharp

[⬆ SysWeaver overview](../../README.md)

> Codec plug-in: **zstd** (Zstandard) backed by the fully managed ZstdSharp.Port.

| | |
|---|---|
| **Layer** | Compression |
| **Kind** | Plug-in (codec, `ICompType`) |

## Purpose

Adds the Zstandard format, giving a fast codec with a good ratio for storage, transfer and HTTP clients that support `zstd`, without native binaries.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Manifest["manifest entry or Register call"] --> CM["CompManager<br/>registry by HTTP code"]
  Plugin["ZstdSharp"] --> CM
  CM -->|best priority per HTTP code| Users["HTTP server, web assets,<br/>storage, remote calls"]
```

All compressed data in SysWeaver — HTTP content encoding, pre-compressed web assets, stored values, compressed API parameters — goes through the codec registry in [SysWeaver.Compression](../../SysWeaver.Compression/README.md). Adding a plug-in makes its codec available everywhere at once; clients choose it through the HTTP `Accept-Encoding` header.

## Key features

- Registered for `zstd` (file extension `.zstd`) with priority 1.
- `CompZstdSharp` (singleton `Instance`) implements the full `ICompType` API through `CompStreamCodec`, with pooled buffers.
- Effort levels map to zstd level 1 / 9 / 22; compressor and decompressor contexts are pooled and reused.
- Decodes concatenated (and skippable) zstd frames.
- Registered through the manifest like any service, or with one static call in code.

## Key components

- [`CompZstdSharp`](CompZstdSharp.cs) — the codec type

## Using it

The service manager recognises codec types and registers them instead of creating an instance:

```json
{ "Type": "SysWeaver.Compression.CompZstdSharp, SysWeaver.Compression.ZstdSharp" }
```

Or register it from code at startup: `SysWeaver.Compression.CompZstdSharp.Register();`

## Limitations and considerations

- Browser support for `zstd` content encoding is newer than for Brotli/GZip; the server falls back to other codecs when a client does not accept it.
- The `Best` level (22) is very slow and memory hungry for large inputs; use it for offline compression only.
- Selection between codecs of the same HTTP code is by priority; this is currently the only registered `zstd` implementation (the .NET 11+ `CompZStdNETNew` in [SysWeaver.Compression](../../SysWeaver.Compression/README.md) is not registered by default).

## Relationships

- **Project:** [`SysWeaver.Compression.ZstdSharp.csproj`](SysWeaver.Compression.ZstdSharp.csproj)
- **Builds on:** [SysWeaver.Compression](../../SysWeaver.Compression/README.md)
- **Used by:** in SysWeaver.Services: [SwServerManager](https://github.com/dosh1974/SysWeaver.Services/blob/main/SwServerManager/README.md); in SysWeaver.Tools: [Compress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Compress/README.md), [Decompress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Decompress/README.md)
- **Third-party:** [ZstdSharp.Port](https://www.nuget.org/packages/ZstdSharp.Port)
