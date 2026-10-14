# SysWeaver.Compression

[⬆ SysWeaver overview](../README.md)

> The compression abstraction and registry: named, prioritised codecs (Brotli, Deflate, GZip built in; Zstandard and native Brotli as plug-ins) used for HTTP content encoding, pre-compressed web assets, storage and data transfer.

| | |
|---|---|
| **Layer** | Compression |
| **Kind** | Library |

## Purpose

Give the whole framework a single way to compress and decompress data by HTTP encoding name ("br", "gzip", "deflate", "zstd") with selectable effort levels, so that the web server, the build-time asset pipeline, key/value storage, chunk storage and remote calls all share codecs.

## How it fits into SysWeaver

```mermaid
flowchart TB
  Builtin[".NET Brotli, Deflate, GZip"] --> CM["CompManager<br/>codecs by HTTP code"]
  Native["Native Brotli plug-in"] --> CM
  Zstd["Zstandard plug-in"] --> CM
  CM --> Http["HTTP server<br/>Accept-Encoding negotiation"]
  CM --> Assets["Embedded web assets<br/>pre-compressed .br"]
  CM --> Storage["Key/value store, chunk store"]
  CM --> Api["Compressed API parameters"]
```

## Key features

- Codec lookup by HTTP content-coding name or file extension (`CompManager.GetFromHttp`, `CompManager.GetFromExt`); the highest priority implementation wins (on a tie, the last registered).
- Built-in codecs registered by default: `CompBrotliNETNew` (`br`), `CompDeflateNET` (`deflate`) and `CompGZipNET` (`gzip`, extensions `gz` / `gzip`), all priority 0. An alternative `BrotliStream` based codec (`CompBrotliNET`, priority -1) and codecs using the .NET 11+ encoder APIs (only compiled when targeting .NET 11 or later) exist but are not registered.
- Three effort levels (`CompEncoderLevels.Fast` / `Balanced` / `Best`), mapped by each codec to its own quality setting (ex: Brotli quality 1 / 4 / 11). Callers express preferences as compact strings such as `"br:Balanced, gzip:Fast"`.
- Sync and async APIs for every combination of stream / memory input and output; extension methods (`CompExt`: `GetCompressed`, `GetDecompressed`, `GetDecompressedArray`, `GetUnmanagedDecompressed`) that compress or decompress into an exact size array or pooled memory.
- Truncated or invalid compressed data throws (`InvalidDataException`) instead of silently returning partial data, also for the .NET `DeflateStream` / `GZipStream` based codecs.
- Helpers for embedded resources that may be stored pre-compressed (`AsmResExt`, ex: `index.html.br`) and for text files on disc that may be compressed (`CompFile`).
- Conversion of GZip data to raw Deflate without recompression (`TransformGZipToDeflateStream`).
- `CompStreamCodec<TEncoder, TDecoder>` implements the whole codec API on top of a struct encoder / decoder pair with pooled buffers (used by the Brotli, native Brotli and Zstandard codecs); `CompInstancePool<T>` reuses native encoder / decoder state.

## Key components

- [`ICompType`](ICompType.cs) — the codec contract (name, HTTP code, priority, file extensions, compress / decompress)
- [`CompManager`](CompManager.cs) — the process-wide codec registry
- [`CompExt`](CompExt.cs) — compress / decompress extension methods
- [`CompStreamCodec`](CompStreamCodec.cs) — building blocks for implementing a codec from a streaming encoder / decoder
- [`AsmResExt`](AsmResExt.cs) — read (optionally pre-compressed) embedded resources

## Using it

Built-in codecs need no registration. Add plug-ins via the manifest (they are registered, not instantiated), or call their static `Register()` method:

```json
{ "Type": "SysWeaver.Compression.CompZstdSharp, SysWeaver.Compression.ZstdSharp" }
```

```csharp
var br = CompManager.GetFromHttp("br");
var packed = br.GetCompressed(data, CompEncoderLevels.Balanced);
```

## Limitations and considerations

- Codec availability is process-wide; clients can only negotiate codecs that have been registered.
- Concatenated streams are only decoded for GZip (members) and Zstandard (frames); for other formats data after the first compressed stream is ignored.
- When decompressing from a stream, the decoder may read past the end of the compressed data.
- Build steps that pre-compress web assets use an external tool (see [SysWeaver overview](../README.md) build notes).
- Platforms: this library and the Zstandard plug-in are fully managed; the native Brotli plug-in has no arm64 binaries, see [SysWeaver.Compression.BrotliNativeNET](../Compression/SysWeaver.Compression.BrotliNativeNET/README.md).

## Relationships

- **Project:** [`SysWeaver.Compression.csproj`](SysWeaver.Compression.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.Auth.Core](../SysWeaver.Auth.Core/README.md), [SysWeaver.Compression.BrotliNativeNET](../Compression/SysWeaver.Compression.BrotliNativeNET/README.md), [SysWeaver.Compression.ZstdSharp](../Compression/SysWeaver.Compression.ZstdSharp/README.md), [SysWeaver.DbSimpleStack](../SysWeaver.DbSimpleStack/README.md), [SysWeaver.FolderSync](../SysWeaver.FolderSync/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.Knowledge](../SysWeaver.Knowledge/README.md), [SysWeaver.LanguageIdentifier.FastText](../SysWeaver.LanguageIdentifier.FastText/README.md), [SysWeaver.Map](../SysWeaver.Map/README.md), [SysWeaver.Media.Svg](../SysWeaver.Media.Svg/README.md), [SysWeaver.MicroService.Media](../SysWeaver.MicroService.Media/README.md), [SysWeaver.MicroService.TranslationDbCache](../SysWeaver.MicroService.TranslationDbCache/README.md), [SysWeaver.Remote.Connection](../SysWeaver.Remote.Connection/README.md), [SysWeaver.Storage](../SysWeaver.Storage/README.md), [SysWeaver.TableData](../SysWeaver.TableData/README.md); in SysWeaver.Tools: [Compress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Compress/README.md), [Decompress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Decompress/README.md), [SvgOpt](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SvgOpt/README.md), [SwCurl](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwCurl/README.md), [SwSyncTool](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwSyncTool/README.md)
- **Third-party:** [CommunityToolkit.HighPerformance](https://www.nuget.org/packages/CommunityToolkit.HighPerformance)
