# SimpleStack.Orm

[⬆ src](../README.md)

> The dialect-independent core of the vendored SimpleStack.Orm: a lightweight ORM on top of Dapper that maps one class to one table and generates parameterised SQL from lambda expressions.

| | |
|---|---|
| **Project** | SimpleStack.Orm ([`SimpleStack.Orm.csproj`](SimpleStack.Orm.csproj)) |
| **Kind** | Vendored library project (.NET Standard 2.0) |
| **Used by SysWeaver** | Yes, listed in `SysWeaver.slnx` and referenced by [SysWeaver.DbSimpleStack](../../../../SysWeaver.DbSimpleStack/README.md) |

## Purpose

Contains everything that does not depend on a particular database: the connection wrapper with its synchronous and asynchronous CRUD methods, the model definitions built from a class and its attributes, the expression visitors and statement builders that turn lambdas into SQL, the type mapper and dialect provider base classes, and a minimal logging abstraction. The dialect projects next to it (in SysWeaver only [SimpleStack.Orm.MySQLConnector](../SimpleStack.Orm.MySQLConnector/README.md)) derive from `DialectProviderBase` and `DbTypeMapperBase`.

The copy has been modified in the SysWeaver repository (see the git history of this folder), so it is not identical to the upstream NuGet package. Package references: `Dapper` and `Microsoft.CSharp`.

## Contents

| Item | Description |
|---|---|
| [`Attributes/`](Attributes/README.md) | Attributes that tune table and column mapping (alias, primary key, index, foreign key, ...) |
| [`Expressions/`](Expressions/README.md) | Expression visitors and statement builders that generate SQL from lambda expressions |
| [`Logging/`](Logging/README.md) | Minimal logger interfaces with debug and no-op implementations |
| [`Properties/`](Properties/README.md) | Assembly attributes, including `InternalsVisibleTo` for the dialect projects |
| [`ColumnDefinition.cs`](ColumnDefinition.cs) | `ColumnType` / `ColumnDefinition`: column information read back from a database |
| [`ConfigExtensions.cs`](ConfigExtensions.cs) | Builds and caches the `ModelDefinition` of a type (per type and table name) from its properties and attributes |
| [`DbTypeMapper.cs`](DbTypeMapper.cs) | `IDbTypeMapper` and `DbTypeMapperBase`: maps .NET types to database column types |
| [`DialectProviderBase.cs`](DialectProviderBase.cs) | Base class for dialects: SQL generation for tables, indices, CRUD and schema queries |
| [`EnumAliasTypeHandler.cs`](EnumAliasTypeHandler.cs) | Dapper type handler for enums stored by their alias |
| [`FieldDefinition.cs`](FieldDefinition.cs) | Mapping information for one property / column |
| [`ForeignKeyConstraint.cs`](ForeignKeyConstraint.cs) | Foreign key definition used when creating tables |
| [`IDialectProvider.cs`](IDialectProvider.cs) | `IDialectProvider` interface and the `OnFkOption` enum |
| [`INamingStrategy.cs`](INamingStrategy.cs) | Interface for mapping type and property names to table and column names |
| [`ITypeHandlerColumnType.cs`](ITypeHandlerColumnType.cs) | Dapper type handler that also states its column type |
| [`ModelDefinition.cs`](ModelDefinition.cs) | Mapping information for a class / table, with a cached generic accessor `ModelDefinition<T>` |
| [`NamingStrategyBase.cs`](NamingStrategyBase.cs) | Default naming strategy |
| [`OrmCommand.cs`](OrmCommand.cs) | Internal `DbCommand` wrapper |
| [`OrmConnection.cs`](OrmConnection.cs) | `OrmConnection`: a `DbConnection` wrapper bound to a dialect provider |
| [`OrmConnectionAsync.cs`](OrmConnectionAsync.cs) | Asynchronous query, insert, update, delete, count and schema methods of `OrmConnection` |
| [`OrmConnectionFactory.cs`](OrmConnectionFactory.cs) | Creates and opens `OrmConnection`s from a dialect provider and a connection string |
| [`OrmConnectionSync.cs`](OrmConnectionSync.cs) | Synchronous counterparts of the connection methods |
| [`OrmException.cs`](OrmException.cs) | Exception type of the library |
| [`OrmTransaction.cs`](OrmTransaction.cs) | `DbTransaction` wrapper |
| [`PlatformExtensions.cs`](PlatformExtensions.cs) | Reflection helpers (public properties, attribute lookup, safe variable names) |
| [`PropertyIndexer.cs`](PropertyIndexer.cs) | `PropertyInvoker`: compiled property getter delegates |
| [`SimpleStack.Orm.csproj`](SimpleStack.Orm.csproj) | Project file (`netstandard2.0`) |
| [`TableDefinition.cs`](TableDefinition.cs) | Table information read back from a database |
| [`UpdateAggregations.cs`](UpdateAggregations.cs) | `UpdateAggregations` enum: how a column is aggregated on upsert (e.g. add the inserted value) |

## Relationships

- **Part of:** [simplestack.orm](../../README.md), vendored under [_external](../../../README.md)
- **Used by:** [SysWeaver.DbSimpleStack](../../../../SysWeaver.DbSimpleStack/README.md) (which also declares its own column attributes in the `SimpleStack.Orm.Attributes` namespace), and every dialect project in [`src/`](../README.md)
