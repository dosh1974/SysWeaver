# SysWeaver.Compression.BrotliNativeNET

[⬆ SysWeaver overview](../README.md)

> Brotli codec plug-in based on the native Brotli library, preferred over the built-in .NET Brotli codec when registered.

| | |
|---|---|
| **Layer** | Compression |
| **Kind** | Codec plug-in (`ICompType`) |

## Purpose

An alternative `br` implementation (`CompBrotliNativeNET`) that calls the native Brotli library of the [Brotli.NET](https://www.nuget.org/packages/Brotli.NET) package directly (bypassing its managed stream), registered with priority 1, above the built-in .NET codec (priority 0). The framework's asset compression tool (`_tools/compress`) ships with it.

## How it fits into SysWeaver

It registers into [SysWeaver.Compression](../SysWeaver.Compression/README.md)'s codec registry under `br`; every consumer that asks for Brotli then uses it.

## Key features

- Implements the full `ICompType` API through `CompStreamCodec`, with pooled buffers.
- Effort levels map to Brotli quality 1 / 4 / 11, with a 2^22 byte window.

## Limitations and considerations

- Depends on native binaries, which must match the process architecture and platform.
- A native encoder / decoder state is created and destroyed per call (no pooling, no one-shot API).
- Concatenated Brotli streams are not decoded (data after the first stream is ignored).

## Using it

```json
{ "Type": "SysWeaver.Compression.CompBrotliNativeNET, SysWeaver.Compression.BrotliNativeNET" }
```

## Relationships

- **Project:** [`SysWeaver.Compression.BrotliNativeNET.csproj`](SysWeaver.Compression.BrotliNativeNET.csproj)
- **Builds on:** [SysWeaver.Compression](../SysWeaver.Compression/README.md)
