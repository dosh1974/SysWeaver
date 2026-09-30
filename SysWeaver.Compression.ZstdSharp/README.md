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

- Pure managed implementation (no native binaries).
- Once registered, available for HTTP content negotiation and for compressed binary API parameters.

## Limitations and considerations

- Browser support for `zstd` content encoding is newer than for Brotli/GZip; the server falls back to other codecs when a client does not accept it.

## Using it

```json
{ "Type": "SysWeaver.Compression.CompZstdSharp, SysWeaver.Compression.ZstdSharp" }
```

## Relationships

- **Project:** [`SysWeaver.Compression.ZstdSharp.csproj`](SysWeaver.Compression.ZstdSharp.csproj)
- **Builds on:** [SysWeaver.Compression](../SysWeaver.Compression/README.md)
