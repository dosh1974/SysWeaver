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

- Depends on native binaries, which must match the process architecture and platform. The Brotli.NET package (2.1.1, the latest) only ships
  win-x64, win-x86, linux-x64, linux-x86 and osx-x64 binaries: there is none for linux-arm64, win-arm64 or osx-arm64.
  `CompBrotliNativeNET.IsSupported` tells if the native library loads; `Register()` does nothing when it doesn't (the built-in `br` codec is then used),
  calling the codec directly then throws a `TypeInitializationException`.
- Linux: the native library only needs libc (tested on Ubuntu 24.04 x64 and Alpine x64). The Brotli.NET loader imports dlopen from "libdl.so",
  which only exists with the libc6-dev package; this plug-in maps it to "libdl.so.2" / "libc.so.6" (a `NativeLibrary.SetDllImportResolver` for the
  Brotli.NET assembly, registered when this assembly loads; compression-cov-1). If an application registers its own resolver for Brotli.NET first, that one is used.
- A native encoder / decoder state is created and destroyed per call (no pooling, no one-shot API).
- Concatenated Brotli streams are not decoded (data after the first stream is ignored).

## Using it

```json
{ "Type": "SysWeaver.Compression.CompBrotliNativeNET, SysWeaver.Compression.BrotliNativeNET" }
```

## Relationships

- **Project:** [`SysWeaver.Compression.BrotliNativeNET.csproj`](SysWeaver.Compression.BrotliNativeNET.csproj)
- **Builds on:** [SysWeaver.Compression](../SysWeaver.Compression/README.md)
