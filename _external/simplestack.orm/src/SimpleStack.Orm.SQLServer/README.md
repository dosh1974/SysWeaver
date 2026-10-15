# SimpleStack.Orm.SQLServer

[⬆ src](../README.md)

> The Microsoft SQL Server dialect of the vendored SimpleStack.Orm, based on Microsoft.Data.SqlClient; not used by SysWeaver.

| | |
|---|---|
| **Project** | SimpleStack.Orm.SQLServer ([`SimpleStack.Orm.SQLServer.csproj`](SimpleStack.Orm.SQLServer.csproj)) |
| **Kind** | Vendored library project (.NET Standard 2.0) |
| **Used by SysWeaver** | No, not in `SysWeaver.slnx` and not referenced by any SysWeaver project; carried along unused |

## Purpose

Implements the [core](../SimpleStack.Orm/README.md) dialect and type mapper base classes for SQL Server.

## Contents

| Item | Description |
|---|---|
| [`Properties/`](Properties/README.md) | Assembly attributes |
| [`SimpleStack.Orm.SQLServer.csproj`](SimpleStack.Orm.SQLServer.csproj) | Project file (`netstandard2.0`, package `Microsoft.Data.SqlClient`) |
| [`SqlServerOrmLiteDialectProvider.cs`](SqlServerOrmLiteDialectProvider.cs) | `SqlServerDialectProvider` (the file name differs from the class name): SQL generation and schema queries for SQL Server |
| [`SqlServerTypeMapper.cs`](SqlServerTypeMapper.cs) | Maps .NET types to SQL Server column types |

## Relationships

- **Part of:** [simplestack.orm](../../README.md), vendored under [_external](../../../README.md)
- **Uses:** [SimpleStack.Orm](../SimpleStack.Orm/README.md)
- **Referenced by:** the upstream tests in [`test/`](../../test/README.md) only
