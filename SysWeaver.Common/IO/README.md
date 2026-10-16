# IO

[⬆ SysWeaver.Common](../README.md)

> Stream and binary reader helpers: endianness aware readers and a length limited view of a stream.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.IO`) |

## Purpose

Reading binary formats that are stored in a fixed byte order (e.g. big endian file formats, or data written on another machine) and reading a bounded part of a larger stream.

## Contents

| Item | Description |
|---|---|
| [`EndianAwareBinaryReader.cs`](EndianAwareBinaryReader.cs) | Factory methods creating a `BinaryReader` for little, big, current or a specified endianness, and the `Endianess` enum |
| [`ReversedEndianBinaryReader.cs`](ReversedEndianBinaryReader.cs) | A `BinaryReader` that reads multi byte numbers in the reverse byte order of the machine |
| [`LengthLimitedStream.cs`](LengthLimitedStream.cs) | A read only view of another stream, starting at its current position and limited to a maximum length |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.Inspection.Binary](../../SysWeaver.Inspection.Binary/README.md), [SysWeaver.Media.Png](../../SysWeaver.Media.Png/README.md)
