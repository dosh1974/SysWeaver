# SysWeaver.Compression.ZstdSharp

[⬆ SysWeaver overview](../README.md)

> Zstandard (`zstd`) codec plug-in, fully managed.

| | |
|---|---|
| **Layer** | Compression |
| **Kind** | Codec plug-in (`ICompType`) |

## Purpose

Adds the Zstandard format to [SysWeaver.Compression](../SysWeaver.Compression/README.md), giving a fast codec with a good ratio for storage, transfer and HTTP clients that support `zstd`.

## Key features

- `CompZstdSharp`: pure managed implementation based on [ZstdSharp.Port](https://www.nuget.org/packages/ZstdSharp.Port) (no native binaries), registered as `zstd` (file extension `.zstd`) with priority 1.
- Effort levels map to zstd level 1 / 9 / 22; compressor and decompressor contexts are pooled and reused.
- Decodes concatenated (and skippable) zstd frames.
- Once registered, available for HTTP content negotiation and for compressed binary API parameters.

## Limitations and considerations

- Browser support for `zstd` content encoding is newer than for Brotli/GZip; the server falls back to other codecs when a client does not accept it.
- The `Best` level (22) is very slow and memory hungry for large inputs; use it for offline compression only.

## Using it

```json
{ "Type": "SysWeaver.Compression.CompZstdSharp, SysWeaver.Compression.ZstdSharp" }
```

## Relationships

- **Project:** [`SysWeaver.Compression.ZstdSharp.csproj`](SysWeaver.Compression.ZstdSharp.csproj)
- **Builds on:** [SysWeaver.Compression](../SysWeaver.Compression/README.md)
