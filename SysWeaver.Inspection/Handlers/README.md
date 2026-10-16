# Handlers

[⬆ SysWeaver.Inspection](../README.md)

> Internal building blocks used when compiling type handlers for arrays, enums, collections and special value types.

| | |
|---|---|
| **Project** | [SysWeaver.Inspection](../README.md) |
| **Kind** | Source code (internal, namespace `SysWeaver.Inspection.Implementation`) |

## Purpose

When `TypeHandler<T>` builds the expression tree for a type that cannot simply be inspected field by field, it uses the creators and describe methods in this folder. The creator classes return expression tree delegates (`MakeCreator`, `MakeDescriptor`, `MakeWrappedDescriptor`); the collection and value type classes contain the `Describe_*` methods that the generated code calls.

## Contents

| Item | Description |
|---|---|
| [`ArrayTypeHandlerCreator.cs`](ArrayTypeHandlerCreator.cs) | Creation and description of arrays |
| [`EnumTypeHandler.cs`](EnumTypeHandler.cs) | Creation and description of enums (`EnumTypeHandlerCreator<T>`) |
| [`CollectionTypeHandlers.cs`](CollectionTypeHandlers.cs) | Description of non generic lists (`IList`) |
| [`GenericCollectionTypeHandlers.cs`](GenericCollectionTypeHandlers.cs) | Description of generic `IList<E>` and `ICollection<E>` |
| [`ValueTypeHandlers.cs`](ValueTypeHandlers.cs) | Value types that cannot be described by their fields: `Nullable<U>` and `KeyValuePair<K, V>` |

## Relationships

- **Part of:** [SysWeaver.Inspection](../README.md)
- **Used by:** [`TypeHandler.cs`](../TypeHandler.cs)
