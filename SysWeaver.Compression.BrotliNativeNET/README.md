# SysWeaver.Compression.BrotliNativeNET

[⬆ SysWeaver overview](../README.md)

> Brotli codec plug-in based on the native Brotli library, preferred over the built-in .NET Brotli codec when registered.

| | |
|---|---|
| **Layer** | Compression |
| **Kind** | Codec plug-in (`ICompType`) |

## Purpose

An alternative `br` implementation using a native Brotli library, registered with a higher priority than the built-in .NET codec. The framework's asset compression tool ships with it.

## How it fits into SysWeaver

It registers into [SysWeaver.Compression](../SysWeaver.Compression/README.md)'s codec registry under `br`; every consumer that asks for Brotli then uses it.

## Limitations and considerations

- Depends on native binaries, which must match the process architecture and platform.

## Using it

```json
{ "Type": "SysWeaver.Compression.CompBrotliNativeNET, SysWeaver.Compression.BrotliNativeNET" }
```

## Relationships

- **Project:** [`SysWeaver.Compression.BrotliNativeNET.csproj`](SysWeaver.Compression.BrotliNativeNET.csproj)
- **Builds on:** [SysWeaver.Compression](../SysWeaver.Compression/README.md)
