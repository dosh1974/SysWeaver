# SimpleStack.Orm.MySQL

[⬆ src](../README.md)

> The MySQL dialect of the vendored SimpleStack.Orm based on Oracle's MySql.Data driver; not used by SysWeaver, apart from one shared source file.

| | |
|---|---|
| **Project** | SimpleStack.Orm.MySQL ([`SimpleStack.Orm.MySQL.csproj`](SimpleStack.Orm.MySQL.csproj)) |
| **Kind** | Vendored library project (.NET Standard 2.0) |
| **Used by SysWeaver** | No, not in `SysWeaver.slnx` and not referenced by any SysWeaver project; carried along unused |

## Purpose

Upstream alternative to [SimpleStack.Orm.MySQLConnector](../SimpleStack.Orm.MySQLConnector/README.md), using the `MySql.Data` package instead of `MySqlConnector`. SysWeaver uses the MySqlConnector variant instead.

[`ColumnsInformationSchema.cs`](ColumnsInformationSchema.cs) is the exception: the MySqlConnector project includes it as a linked source file, so it is compiled into SysWeaver even though this project is not.

## Contents

| Item | Description |
|---|---|
| [`Properties/`](Properties/README.md) | Assembly attributes |
| [`ColumnsInformationSchema.cs`](ColumnsInformationSchema.cs) | `InformationSchema`: row class for MySQL `information_schema` column queries (also compiled into SimpleStack.Orm.MySQLConnector) |
| [`MySqlDbTypeMapper.cs`](MySqlDbTypeMapper.cs) | Maps .NET types to MySQL column types |
| [`MySqlDialectProvider.cs`](MySqlDialectProvider.cs) | `MySqlDialectProvider`: SQL generation and schema queries for MySQL |
| [`SimpleStack.Orm.MySQL.csproj`](SimpleStack.Orm.MySQL.csproj) | Project file (`netstandard2.0`, package `MySql.Data`) |

## Limitations and considerations

- The project file declares a GPL 2.0 package licence URL (because of `MySql.Data`), unlike the MIT licence of the rest of the library.

## Relationships

- **Part of:** [simplestack.orm](../../README.md), vendored under [_external](../../../README.md)
- **Uses:** [SimpleStack.Orm](../SimpleStack.Orm/README.md)
- **Shares code with:** [SimpleStack.Orm.MySQLConnector](../SimpleStack.Orm.MySQLConnector/README.md)
- **Referenced by:** the upstream tests in [`test/`](../../test/README.md) only
