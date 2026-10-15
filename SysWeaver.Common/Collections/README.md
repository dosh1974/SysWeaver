# Collections

[⬆ SysWeaver.Common](../README.md)

> A memory compact, sorted set of single byte (ASCII / Latin-1) strings.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.Collections`) |

## Purpose

`CompactAsciiStringSet` stores a large number of strings in a fraction of the memory of a plain array. Strings are kept in blocks (default 64 strings per block) and front coded: each string after the first in a block is stored as the length of the prefix it shares with the previous string followed by the remaining chars, one byte per char. Lookups (`Contains`, `IndexOf`) do a binary search on the first string of each block followed by a linear scan of the block.

Most of the general purpose collections of the project live in the project root, not in this folder.

## Contents

| Item | Description |
|---|---|
| [`CompactAsciiStringSet.cs`](CompactAsciiStringSet.cs) | The compact string set (`Add`, `Fix`, `Contains`, `IndexOf`, enumeration, memory statistics) |

## Limitations and considerations

- Strings must be added in sorted order (`AsciiCompare`), be non-empty and only contain chars in the range 32–255; this is only validated in DEBUG builds
- Not thread safe; call `Fix()` after the last `Add` before reading from several threads

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
