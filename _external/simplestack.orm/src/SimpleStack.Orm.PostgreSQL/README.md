# SimpleStack.Orm.PostgreSQL

[⬆ src](../README.md)

> The PostgreSQL dialect of the vendored SimpleStack.Orm, based on Npgsql; not used by SysWeaver.

| | |
|---|---|
| **Project** | SimpleStack.Orm.PostgreSQL ([`SimpleStack.Orm.PostgreSQL.csproj`](SimpleStack.Orm.PostgreSQL.csproj)) |
| **Kind** | Vendored library project (.NET Standard 2.0) |
| **Used by SysWeaver** | No, not in `SysWeaver.slnx` and not referenced by any SysWeaver project; carried along unused |

## Purpose

Implements the [core](../SimpleStack.Orm/README.md) dialect and type mapper base classes for PostgreSQL, including a naming strategy for PostgreSQL table and column names.

## Contents

| Item | Description |
|---|---|
| [`Properties/`](Properties/README.md) | Assembly attributes |
| [`ColumnInformationSchema.cs`](ColumnInformationSchema.cs) | Row class for PostgreSQL `information_schema` column queries |
| [`PostgreSQLDialectProvider.cs`](PostgreSQLDialectProvider.cs) | `PostgreSQLDialectProvider`: SQL generation and schema queries for PostgreSQL |
| [`PostgreSQLTypeMapper.cs`](PostgreSQLTypeMapper.cs) | Maps .NET types to PostgreSQL column types |
| [`PostgreSqlNamingStrategy.cs`](PostgreSqlNamingStrategy.cs) | `INamingStrategy` for table and column names |
| [`SimpleStack.Orm.PostgreSQL.csproj`](SimpleStack.Orm.PostgreSQL.csproj) | Project file (`netstandard2.0`, package `Npgsql`) |
| [`SimpleStack.Orm.PostgreSQL.csproj.DotSettings`](SimpleStack.Orm.PostgreSQL.csproj.DotSettings) | JetBrains ReSharper / Rider project settings (C# 8 language level) |

## Relationships

- **Part of:** [simplestack.orm](../../README.md), vendored under [_external](../../../README.md)
- **Uses:** [SimpleStack.Orm](../SimpleStack.Orm/README.md)
- **Referenced by:** the upstream tests in [`test/`](../../test/README.md) only
