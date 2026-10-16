# Reader

[⬆ SysWeaver.Serialization.SwJson](../README.md)

> Internals of the SysWeaver JSON reader: pointer-based UTF-8 parsing, primitive parsers and the compiled per-type readers behind `JsonReader`.

| | |
|---|---|
| **Project** | [SysWeaver.Serialization.SwJson](../README.md) |
| **Kind** | Source code |

## Purpose

[`JsonReader`](../JsonReader.cs) (in the project root) is the public entry point; everything it needs lives here. Parsing works directly on pinned UTF-8 bytes through raw pointers, with a pooled [`JsonParserState`](JsonParserState.cs) per parse. For each target type, [`ReadTypeCache`](ReadTypeCache.cs) builds a reader from compiled expression trees on first use and keeps it for the life of the process; the generated code calls the token level parser ([`Utf8JsonParser`](Utf8JsonParser.cs)), the low level parser ([`Utf8Parser`](Utf8Parser.cs)) and the primitive parsers ([`SpanParsers`](SpanParsers.cs), [`FusedParsers`](FusedParsers.cs)). All parsing is culture invariant.

## Contents

| Item | Description |
|---|---|
| [`Utf8Parser.cs`](Utf8Parser.cs) | Low level UTF-8 parsing on raw pointers: white space and comment skipping, ranges, strings with and without escapes, base64 (UTF-8 is not validated) |
| [`Utf8JsonParser.cs`](Utf8JsonParser.cs) | Token level reading: null detection, strings, keys, enums, type names, maybe-quoted scalars and skipping unknown values, with table-based end conditions |
| [`SpanParsers.cs`](SpanParsers.cs) | Parsers from raw bytes to integers, floating point, decimal, dates and times, `Guid` and boolean, with fast paths and fallback to the .NET parse methods, plus the expressions that call them |
| [`FusedParsers.cs`](FusedParsers.cs) | One-pass readers for unquoted numbers and booleans (find the end and parse together), falling back to `SpanParsers` with identical results |
| [`EnumReader.cs`](EnumReader.cs) | Fast enum parsing from UTF-8 (exact names and plain integers), anything else is left to `Enum.Parse` |
| [`DictionaryReader.cs`](DictionaryReader.cs) | Dictionary key parsers (primitive, string and enum keys) from the unescaped key bytes |
| [`MemberLookUp.cs`](MemberLookUp.cs) | Look up of member assigners by UTF-8 member name (case sensitive, exact match) |
| [`Utf8Range.cs`](Utf8Range.cs) | A range of UTF-8 bytes used as a dictionary key for member names (interned instances plus a reusable look up instance) |
| [`ReadTypeCache.cs`](ReadTypeCache.cs) | Builds and caches the compiled reader for a type (primitives, special cases, enums, arrays, collections, nullables, enumerables, reference types and dictionaries, other value types) |
| [`ReadTyped.cs`](ReadTyped.cs) | Per type static generic storage of the compiled readers: value creator, object factory, member assigners and dictionary item adder |
| [`JsonParserState.cs`](JsonParserState.cs) | The state of one parse (data pointers, temp buffers, reusable look up objects); pooled, max 32, not thread safe |
| [`UnmanagedMemoryManager.cs`](UnmanagedMemoryManager.cs) | A `MemoryManager<T>` over a raw pointer, to get a `Memory<T>` over the json data for dictionary look ups without copying |

## Relationships

- **Part of:** [SysWeaver.Serialization.SwJson](../README.md)
- **Used by:** [`JsonReader`](../JsonReader.cs), and through it [`SysWeaverJsonSerializer`](../SysWeaverJsonSerializer.cs)
- **Counterpart:** [`Writer/`](../Writer/README.md)
