# SimpleStack.Orm.Tests

[⬆ test](../README.md)

> Upstream NUnit test suite of SimpleStack.Orm that runs the same tests against every dialect; not used by SysWeaver.

| | |
|---|---|
| **Project** | SimpleStack.Orm.Tests ([`SimpleStack.Orm.Tests.csproj`](SimpleStack.Orm.Tests.csproj)) |
| **Kind** | Vendored test project (NUnit, `netcoreapp3.1`) |
| **Used by SysWeaver** | No, not in `SysWeaver.slnx`; carried along unused |

## Purpose

The tests are written as the abstract partial class `ExpressionTests`, spread over many files. [`ExpressionTests.cs`](ExpressionTests.cs) defines one concrete fixture per dialect (PostgreSQL, MySqlConnector, MySQL, SQL Server, SQLite and System.Data.SQLite), each with a hard-coded local connection string, so running them requires the corresponding database servers. The project references the core and all dialect projects in [`src/`](../../src/README.md).

## Contents

| Item | Description |
|---|---|
| [`Properties/`](Properties/README.md) | Assembly attributes |
| [`ExpressionTests.cs`](ExpressionTests.cs) | Abstract base fixture (test models, custom type handlers, setup) and the per-dialect fixtures |
| [`TestType.cs`](TestType.cs), [`ConsoleLoggerFactory.cs`](ConsoleLoggerFactory.cs) | Test model and a console logger factory for the ORM logging interfaces |
| [`AdditiveExpressionsTest.cs`](AdditiveExpressionsTest.cs), [`ConditionalExpressionTests.cs`](ConditionalExpressionTests.cs), [`EqualityExpressionTests.cs`](EqualityExpressionTests.cs), [`LogicalExpressions.cs`](LogicalExpressions.cs), [`MultiplicativeExpressionTests.cs`](MultiplicativeExpressionTests.cs), [`PrimaryExpressionTests.cs`](PrimaryExpressionTests.cs), [`RelationalExpressionTests.cs`](RelationalExpressionTests.cs), [`StringExpressionTests.cs`](StringExpressionTests.cs), [`UnaryExpressionsTest.cs`](UnaryExpressionsTest.cs) | Translation of the different C# expression kinds to SQL |
| [`ExpressionChainingUseCases.cs`](ExpressionChainingUseCases.cs), [`ExpressionVisitorTests.cs`](ExpressionVisitorTests.cs) | Chained conditions and visitor behaviour |
| [`JoinSqlBuilderTests.cs`](JoinSqlBuilderTests.cs) | Join builder tests, entirely commented out |
| [`AliasTests.cs`](AliasTests.cs), [`TestReservedWordAlias.cs`](TestReservedWordAlias.cs), [`DefaultValueTests.cs`](DefaultValueTests.cs), [`DbTypeMapperTests.cs`](DbTypeMapperTests.cs) | Mapping: aliases, reserved words, default values, type mapping |
| [`AutonumInsertTests.cs`](AutonumInsertTests.cs), [`UpdateTests.cs`](UpdateTests.cs), [`DeleteTests.cs`](DeleteTests.cs), [`CountTests.cs`](CountTests.cs), [`SelectCountTests.cs`](SelectCountTests.cs), [`DynamicSelectStatement.cs`](DynamicSelectStatement.cs) | CRUD, counting and dynamic queries |
| [`AsynchronousTests.cs`](AsynchronousTests.cs), [`TransactionTests.cs`](TransactionTests.cs) | Async API and transactions |
| [`GetTableColumnsTests.cs`](GetTableColumnsTests.cs), [`GetTablesListTests.cs`](GetTablesListTests.cs) | Schema queries (tables and columns) |
| [`PerfTests.cs`](PerfTests.cs) | Simple performance tests |
| [`SimpleStack.Orm.Tests.csproj`](SimpleStack.Orm.Tests.csproj) | Project file (NUnit, references all `src/` projects) |
| [`packages.config`](packages.config) | Legacy NuGet package list for .NET Framework 4.5 (old versions; not used by the SDK-style project) |

## Relationships

- **Part of:** [`test/`](../README.md) of [simplestack.orm](../../README.md)
- **Tests:** every project in [`src/`](../../src/README.md)
