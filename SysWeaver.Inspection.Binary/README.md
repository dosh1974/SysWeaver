# SysWeaver.Inspection.Binary

[⬆ SysWeaver overview](../README.md)

> Binary reader and writer inspectors: the concrete wire format for SysWeaver's binary serializer.

| | |
|---|---|
| **Layer** | Foundation (serialization infrastructure) |
| **Kind** | Library |

## Purpose

Implements the inspector contracts from [SysWeaver.Inspection](../SysWeaver.Inspection/README.md) against binary streams, producing a compact SysWeaver-specific encoding. It is wrapped as a regular serializer plug-in by [SysWeaver.Serialization.SwBinary](../Serialization/SysWeaver.Serialization.SwBinary/README.md).

## Usage

```csharp
using (var w = new BinaryWriterInspector(stream, leaveOpen: true))
    w.Write(value);                 // written as its declared type
stream.Position = 0;
using var r = new BinaryReaderInspector(stream, leaveOpen: true);
var back = r.Read<MyType>();        // read as the same type (or Read<Object>() if written with saveAsObject: true)
```

The static helpers `BinaryWriterInspector.Write(value, stream, ...)` / `BinaryReaderInspector.Read<T>(stream, ...)` do the same in one call (and can add context values for `IDescribable` implementations).

## Format

- Little endian `BinaryWriter` values; strings are UTF-16 by default (any `Encoding` can be passed, the reader must use the same). With the `BinaryWriter` / `BinaryReader` constructors strings are always UTF-16. Chars are always written as a `UInt16` (any char, also a lone surrogate).
- Objects start with an int: -1 = null, other negative values = a back reference to an object written before (by reference), else the version of the type (bit 30 set when a type name follows, for a runtime type other than the declared type). Objects are numbered in the order they start (before their members), so members can refer back to the object that contains them (except to an object that is created by its `(IInspector, int[, bool])` constructor: it doesn't exist yet, the reference is read as null).
- Strings: -1 = null, other negative values = a back reference to an equal string written before, else the byte length and the bytes.
- Arrays: the version, the lengths of all dimensions, then the elements; `byte[]` as one block.

## Limitations and considerations

- The format is only readable by SysWeaver; use JSON, MessagePack or Protobuf plug-ins for interoperability.
- Type names in the data are resolved with `TypeFinder.GetForData`: the `DataTypePolicy` applies and the type must be assignable to the declared type (else `DataTypeNotAllowedException`), unknown types throw `InvalidDataException`.
- Truncated data throws `EndOfStreamException` (a string of more than 64 KB that is longer than the remaining data throws `InvalidDataException`, without allocating its buffer); a negative string length throws `InvalidDataException`; other corrupt data throws `ArgumentOutOfRangeException` / `OverflowException` (bad back references or lengths) or `InvalidDataException` (unknown type, newer version).
- Objects are always created when reading (the existing value of a member is replaced, not updated).
- Prop methods with a null setter write the value, and skip it when reading.
- `ParentField<T>` / `ParentProp<T>` find the closest parent assignable to T on the reader's `Stack` (the objects being read): an object is pushed when it's created and popped when it's complete, and an object is never its own parent (a tree node's `Parent` is the node above it, null for the root). A struct's parent is the object that contains it. Objects created by their `(IInspector, int[, bool])` constructor are not on the stack while they are read: they find their parent, but the structs / objects read inside their constructor can't find them (they get the next object up). When writing, `ParentField` / `ParentProp` do nothing (parents are not stored).
- `Write(x)` (also through `IWriteInspector`) writes x as its declared type, read it with `Read<T>()`; `Write(x, true)` writes it as an object, read it with `Read<Object>()`.
- Writing never modifies the written objects or collections, several writers may write the same objects at the same time.
- The readers / writers are not thread safe; their look ups are reused by the next reader / writer on the same thread when disposed.

## Known bugs

None known (see `_docs/BugReview-2026-10-06.md`).

## Relationships

- **Project:** [`SysWeaver.Inspection.Binary.csproj`](SysWeaver.Inspection.Binary.csproj)
- **Builds on:** [SysWeaver.Inspection](../SysWeaver.Inspection/README.md)
- **Used by:** [SysWeaver.Serialization.SwBinary](../Serialization/SysWeaver.Serialization.SwBinary/README.md)
