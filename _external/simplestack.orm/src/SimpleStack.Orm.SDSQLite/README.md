# SimpleStack.Orm.SDSQLite

[⬆ src](../README.md)

> The SQLite dialect of the vendored SimpleStack.Orm based on System.Data.SQLite; not used by SysWeaver.

| | |
|---|---|
| **Project** | SimpleStack.Orm.SDSQLite ([`SimpleStack.Orm.SDSQLite.csproj`](SimpleStack.Orm.SDSQLite.csproj)) |
| **Kind** | Vendored library project (.NET Standard 2.0) |
| **Used by SysWeaver** | No, not in `SysWeaver.slnx` and not referenced by any SysWeaver project; carried along unused |

## Purpose

Same role as [SimpleStack.Orm.SQLite](../SimpleStack.Orm.SQLite/README.md), but using the `System.Data.SQLite` driver. The classes have the same names as in the SQLite project and are told apart by namespace (`SimpleStack.Orm.SDSQLite`).

## Contents

| Item | Description |
|---|---|
| [`SimpleStack.Orm.SDSQLite.csproj`](SimpleStack.Orm.SDSQLite.csproj) | Project file (`netstandard2.0`, packages `System.Data.SQLite`, `System.Data.SQLite.Core`, `Microsoft.CSharp`) |
| [`SqliteDialectProvider.cs`](SqliteDialectProvider.cs) | `SqliteDialectProvider`: SQL generation and schema queries for SQLite |
| [`SqliteTypeMapper.cs`](SqliteTypeMapper.cs) | Maps .NET types to SQLite column types |

## Relationships

- **Part of:** [simplestack.orm](../../README.md), vendored under [_external](../../../README.md)
- **Uses:** [SimpleStack.Orm](../SimpleStack.Orm/README.md)
- **Referenced by:** the upstream tests in [`test/`](../../test/README.md) only
