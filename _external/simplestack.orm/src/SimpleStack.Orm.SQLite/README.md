# SimpleStack.Orm.SQLite

[⬆ src](../README.md)

> The SQLite dialect of the vendored SimpleStack.Orm based on Microsoft.Data.Sqlite; not used by SysWeaver.

| | |
|---|---|
| **Project** | SimpleStack.Orm.SQLite ([`SimpleStack.Orm.SQLite.csproj`](SimpleStack.Orm.SQLite.csproj)) |
| **Kind** | Vendored library project (.NET Standard 2.0) |
| **Used by SysWeaver** | No, not in `SysWeaver.slnx` and not referenced by any SysWeaver project; carried along unused |

## Purpose

Implements the [core](../SimpleStack.Orm/README.md) dialect and type mapper base classes for SQLite using `Microsoft.Data.Sqlite`. [SimpleStack.Orm.SDSQLite](../SimpleStack.Orm.SDSQLite/README.md) is the equivalent based on `System.Data.SQLite`.

## Contents

| Item | Description |
|---|---|
| [`Properties/`](Properties/README.md) | Assembly attributes |
| [`SimpleStack.Orm.SQLite.csproj`](SimpleStack.Orm.SQLite.csproj) | Project file (`netstandard2.0`, packages `Microsoft.Data.Sqlite`, `Microsoft.Data.Sqlite.Core`, `Microsoft.CSharp`) |
| [`SqliteDialectProvider.cs`](SqliteDialectProvider.cs) | `SqliteDialectProvider`: SQL generation and schema queries for SQLite |
| [`SqliteTypeMapper.cs`](SqliteTypeMapper.cs) | Maps .NET types to SQLite column types |

## Relationships

- **Part of:** [simplestack.orm](../../README.md), vendored under [_external](../../../README.md)
- **Uses:** [SimpleStack.Orm](../SimpleStack.Orm/README.md)
- **Referenced by:** the upstream tests in [`test/`](../../test/README.md) only
