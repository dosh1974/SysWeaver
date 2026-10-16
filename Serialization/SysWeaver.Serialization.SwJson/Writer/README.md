# Writer

[⬆ SysWeaver.Serialization.SwJson](../README.md)

> Helpers of the SysWeaver JSON writer: the pinned UTF-8 output buffer and fast, culture invariant value formatting.

| | |
|---|---|
| **Project** | [SysWeaver.Serialization.SwJson](../README.md) |
| **Kind** | Source code |

## Purpose

The writer itself, [`JsonWriter`](../JsonWriter.cs) together with [`JsonWriter.Fast.cs`](../JsonWriter.Fast.cs), is in the project root; this folder holds the pieces it writes with. Output goes to a growable, always pinned [`BufferWriter`](BufferWriter.cs) through raw pointers: writers reserve space first and the formatting methods in [`FastFormat`](FastFormat.cs) then write without bounds checks.

## Contents

| Item | Description |
|---|---|
| [`BufferWriter.cs`](BufferWriter.cs) | Growable, pinned byte buffer written through raw pointers (`Ensure` before writing; growing invalidates old pointers; must be disposed to release the pin and rented buffers) |
| [`FastFormat.cs`](FastFormat.cs) | Fast UTF-8 formatting of values to a pointer, culture invariant and documented as identical to the .NET formatting previously used (verified by tests) |
| [`EnumExt.cs`](EnumExt.cs) | Small enumerable helper used only while building the writers (not on the hot path) |

## Relationships

- **Part of:** [SysWeaver.Serialization.SwJson](../README.md)
- **Used by:** [`JsonWriter`](../JsonWriter.cs), and through it [`SysWeaverJsonSerializer`](../SysWeaverJsonSerializer.cs) and [SysWeaver.Serialization.SafeJson](../../SysWeaver.Serialization.SafeJson/README.md)
- **Counterpart:** [`Reader/`](../Reader/README.md)
