# src

[⬆ simplestack.orm](../README.md)

> Source code of the SimpleStack.Orm projects: the dialect-independent ORM core, one project per database dialect and an optional logging adapter.

| | |
|---|---|
| **Library** | [simplestack.orm](../README.md) (vendored) |
| **Kind** | Group of projects |

## Purpose

Every sub folder is a .NET Standard 2.0 class library with its own `.csproj`. All dialect projects and the logging adapter reference the core project [`SimpleStack.Orm`](SimpleStack.Orm/README.md), which builds SQL from lambda expressions and executes it through Dapper.

SysWeaver only builds two of these projects: the core and the MySqlConnector based MySQL dialect. They are the only ones listed in `SysWeaver.slnx` (solution folder `/_external/`) and referenced by [SysWeaver.DbSimpleStack](../../../SysWeaver.DbSimpleStack/README.md) and [SysWeaver.DbSimpleStack.MySql](../../../SysWeaver.DbSimpleStack.MySql/README.md). The other projects are carried along unused.

## Contents

| Item | Used by SysWeaver | Description |
|---|---|---|
| [`SimpleStack.Orm/`](SimpleStack.Orm/README.md) | Yes | ORM core: connections, expression-to-SQL statements, model definitions, attributes, dialect base classes |
| [`SimpleStack.Orm.MySQLConnector/`](SimpleStack.Orm.MySQLConnector/README.md) | Yes | MySQL / MariaDB dialect using the MySqlConnector driver |
| [`SimpleStack.Orm.MySQL/`](SimpleStack.Orm.MySQL/README.md) | No (one file is compiled into the MySqlConnector dialect) | MySQL dialect using Oracle's MySql.Data driver |
| [`SimpleStack.Orm.PostgreSQL/`](SimpleStack.Orm.PostgreSQL/README.md) | No | PostgreSQL dialect using Npgsql |
| [`SimpleStack.Orm.SQLServer/`](SimpleStack.Orm.SQLServer/README.md) | No | SQL Server dialect using Microsoft.Data.SqlClient |
| [`SimpleStack.Orm.SQLite/`](SimpleStack.Orm.SQLite/README.md) | No | SQLite dialect using Microsoft.Data.Sqlite |
| [`SimpleStack.Orm.SDSQLite/`](SimpleStack.Orm.SDSQLite/README.md) | No | SQLite dialect using System.Data.SQLite |
| [`SimpleStack.Orm.Logging.MicrosoftExtensions/`](SimpleStack.Orm.Logging.MicrosoftExtensions/README.md) | No | Adapter from the ORM logging interfaces to Microsoft.Extensions.Logging |

## Relationships

- **Part of:** [simplestack.orm](../README.md), vendored under [_external](../../README.md)
- **Tested by:** [`test/`](../test/README.md) (upstream tests, not used by SysWeaver)
- **Used by:** [SysWeaver.DbSimpleStack](../../../SysWeaver.DbSimpleStack/README.md), [SysWeaver.DbSimpleStack.MySql](../../../SysWeaver.DbSimpleStack.MySql/README.md)
