# SimpleStack.Orm.MySQLConnector

[⬆ src](../README.md)

> The MySQL / MariaDB dialect of the vendored SimpleStack.Orm, built on the MySqlConnector ADO.NET driver; the only dialect SysWeaver uses.

| | |
|---|---|
| **Project** | SimpleStack.Orm.MySQLConnector ([`SimpleStack.Orm.MySQLConnector.csproj`](SimpleStack.Orm.MySQLConnector.csproj)) |
| **Kind** | Vendored library project (.NET Standard 2.0) |
| **Used by SysWeaver** | Yes, listed in `SysWeaver.slnx` and referenced by [SysWeaver.DbSimpleStack.MySql](../../../../SysWeaver.DbSimpleStack.MySql/README.md) |

## Purpose

Implements `DialectProviderBase` and `DbTypeMapperBase` from the [core](../SimpleStack.Orm/README.md) for MySQL: back-tick quoting, schema creation, date part functions, reading table column definitions from `information_schema` and insert statement generation. [SysWeaver.DbSimpleStack.MySql](../../../../SysWeaver.DbSimpleStack.MySql/README.md) uses the shared `MySqlConnectorDialectProvider.Instance` and also derives its own provider from it.

Package reference: `MySqlConnector`. The project also compiles [`ColumnsInformationSchema.cs`](../SimpleStack.Orm.MySQL/ColumnsInformationSchema.cs) from the otherwise unused [SimpleStack.Orm.MySQL](../SimpleStack.Orm.MySQL/README.md) folder as a linked file, so that file must stay in the repository.

## Contents

| Item | Description |
|---|---|
| [`Properties/`](Properties/README.md) | Assembly attributes |
| [`MySqlConnectorDialectProvider.cs`](MySqlConnectorDialectProvider.cs) | `MySqlConnectorDialectProvider`: SQL generation and schema queries for MySQL / MariaDB |
| [`MySqlConnectorTypeMapper.cs`](MySqlConnectorTypeMapper.cs) | Maps .NET types to MySQL column types |
| [`SimpleStack.Orm.MySQLConnector.csproj`](SimpleStack.Orm.MySQLConnector.csproj) | Project file (`netstandard2.0`); links `ColumnsInformationSchema.cs` from `../SimpleStack.Orm.MySQL` |

## Relationships

- **Part of:** [simplestack.orm](../../README.md), vendored under [_external](../../../README.md)
- **Uses:** [SimpleStack.Orm](../SimpleStack.Orm/README.md), `ColumnsInformationSchema.cs` from [SimpleStack.Orm.MySQL](../SimpleStack.Orm.MySQL/README.md)
- **Used by:** [SysWeaver.DbSimpleStack.MySql](../../../../SysWeaver.DbSimpleStack.MySql/README.md)
