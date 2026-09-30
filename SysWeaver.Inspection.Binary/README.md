# SysWeaver.Inspection.Binary

[⬆ SysWeaver overview](../README.md)

> Binary reader and writer inspectors: the concrete wire format for SysWeaver's binary serializer.

| | |
|---|---|
| **Layer** | Foundation (serialization infrastructure) |
| **Kind** | Library |

## Purpose

Implements the inspector contracts from [SysWeaver.Inspection](../SysWeaver.Inspection/README.md) against binary streams, producing a compact SysWeaver-specific encoding. It is wrapped as a regular serializer plug-in by [SysWeaver.Serialization.SwBinary](../Serialization/SysWeaver.Serialization.SwBinary/README.md).

## Limitations and considerations

- The format is only readable by SysWeaver; use JSON, MessagePack or Protobuf plug-ins for interoperability.

## Relationships

- **Project:** [`SysWeaver.Inspection.Binary.csproj`](SysWeaver.Inspection.Binary.csproj)
- **Builds on:** [SysWeaver.Inspection](../SysWeaver.Inspection/README.md)
- **Used by:** [SysWeaver.Serialization.SwBinary](../Serialization/SysWeaver.Serialization.SwBinary/README.md)
