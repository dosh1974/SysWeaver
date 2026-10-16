# Serialization

[⬆ SysWeaver overview](../README.md)

> Groups the serializer plug-in projects that add formats or alternative implementations to the registry of SysWeaver.Serialization.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Group of projects |

## Purpose

[SysWeaver.Serialization](../SysWeaver.Serialization/README.md) defines the serialization abstraction and a registry of named, prioritised serializer types selected by file extension (System.Text.Json and XML are built in). Each project in this folder is an optional plug-in: registering it makes its format available to everything that serializes through the registry, and among serializers for the same extension the highest priority wins. The folder itself only holds the project folders.

## Contents

| Item | Description |
|---|---|
| [`SysWeaver.Serialization.CompactJson/`](SysWeaver.Serialization.CompactJson/README.md) | Serializer plug-in: **json** (text) backed by CompactJson. |
| [`SysWeaver.Serialization.MessagePack/`](SysWeaver.Serialization.MessagePack/README.md) | Serializer plug-in: **msgpack** (binary) backed by MessagePack-CSharp. |
| [`SysWeaver.Serialization.NewtonsoftBson/`](SysWeaver.Serialization.NewtonsoftBson/README.md) | Serializer plug-in: **bson** (binary) backed by Newtonsoft.Json.Bson. |
| [`SysWeaver.Serialization.NewtonsoftJson/`](SysWeaver.Serialization.NewtonsoftJson/README.md) | Serializer plug-in: **json** (text) backed by Newtonsoft.Json. |
| [`SysWeaver.Serialization.ProtobufNet/`](SysWeaver.Serialization.ProtobufNet/README.md) | Serializer plug-in: **proto** (binary) backed by protobuf-net. |
| [`SysWeaver.Serialization.SafeJson/`](SysWeaver.Serialization.SafeJson/README.md) | Serializer plug-in (obsolete): **json** (text) that writes with SysWeaver's fast JSON writer and reads with the tolerant Newtonsoft parser, taking precedence over all other JSON serializers when registered. |
| [`SysWeaver.Serialization.SpanJson/`](SysWeaver.Serialization.SpanJson/README.md) | Serializer plug-in: **json** (text) backed by SpanJson. |
| [`SysWeaver.Serialization.SwBinary/`](SysWeaver.Serialization.SwBinary/README.md) | Serializer plug-in: **swbin** (binary) backed by SysWeaver binary inspectors. |
| [`SysWeaver.Serialization.SwJson/`](SysWeaver.Serialization.SwJson/README.md) | Serializer plug-in: **json** (text) backed by SysWeaver's own JSON reader/writer. |
| [`SysWeaver.Serialization.Utf8Json/`](SysWeaver.Serialization.Utf8Json/README.md) | Serializer plug-in: **json** (text) backed by Utf8Json. |

## Relationships

- **Part of:** [SysWeaver](../README.md)
- **Plug-ins for:** [SysWeaver.Serialization](../SysWeaver.Serialization/README.md)
- **Related:** [SysWeaver.Inspection](../SysWeaver.Inspection/README.md) (the foundation of the `swbin` format)
