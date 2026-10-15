# Inpsection

[⬆ SysWeaver.Common](../README.md)

> The inspection contracts: interfaces and attributes that let an object describe its data to an inspector (reader, writer, copier, ...), the basis of SysWeaver's binary serialization.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (contracts, namespace `SysWeaver.Inspection`) |

## Purpose

The folder name is spelled `Inpsection`; the namespace is `SysWeaver.Inspection`.

An `IInspector` visits the data of an object field by field; the same interface is used for reading (the inspector assigns values), writing (the inspector reads values) and other operations (copy, compare, ...). A type can describe itself by implementing `IDescribable` and declare its version with `[DescVersion]`, so that data written by an older version can still be read. The engine that drives an inspector over arbitrary types is [SysWeaver.Inspection](../../SysWeaver.Inspection/README.md); the binary reader and writer inspectors are in [SysWeaver.Inspection.Binary](../../SysWeaver.Inspection.Binary/README.md).

## Contents

| Item | Description |
|---|---|
| [`IInspector.cs`](IInspector.cs) | The inspector interface (typed `Field` overloads for primitives, strings, dates, `Guid`, ...) and the `SetProp<T>` delegate |
| [`IReadInspector.cs`](IReadInspector.cs) | An inspector that reads (creates) objects from a source |
| [`IWriteInspector.cs`](IWriteInspector.cs) | An inspector that writes objects to a destination |
| [`IDescribable.cs`](IDescribable.cs) | Implemented by types that describe themselves, at the current or an older version |
| [`DescVersionAttribute.cs`](DescVersionAttribute.cs) | The current version of an `IDescribable` type (1 or greater) |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.Inspection](../../SysWeaver.Inspection/README.md), [SysWeaver.Inspection.Binary](../../SysWeaver.Inspection.Binary/README.md); `IDescribable` types e.g. in [SysWeaver.Media.Svg](../../SysWeaver.Media.Svg/README.md)
