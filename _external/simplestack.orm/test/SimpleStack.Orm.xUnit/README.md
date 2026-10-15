# SimpleStack.Orm.xUnit

[⬆ test](../README.md)

> Upstream xUnit tests of SimpleStack.Orm that start database servers in Docker containers and run table creation tests against every dialect; not used by SysWeaver.

| | |
|---|---|
| **Project** | SimpleStack.Orm.xUnit ([`SimpleStack.Orm.xUnit.csproj`](SimpleStack.Orm.xUnit.csproj)) |
| **Kind** | Vendored test project (xUnit, `netcoreapp3.1`) |
| **Used by SysWeaver** | No, not in `SysWeaver.slnx`; carried along unused |

## Purpose

`BaseTest` in [`CreateTableTests.cs`](CreateTableTests.cs) uses Testcontainers to start MySQL (twice, for the MySql.Data and MySqlConnector dialects), PostgreSQL and SQL Server containers, plus two temporary SQLite files, and creates one `OrmConnectionFactory` per dialect. The tests are shared as a class fixture. Running them requires Docker.

## Contents

| Item | Description |
|---|---|
| [`Models/`](Models/README.md) | Test model classes |
| [`Tools/`](Tools/README.md) | Static connection factories for locally installed database servers |
| [`CreateTableTests.cs`](CreateTableTests.cs) | `DialectType` enum, the container-based `BaseTest` fixture and the table creation tests |
| [`SimpleStack.Orm.xUnit.csproj`](SimpleStack.Orm.xUnit.csproj) | Project file (xUnit, Testcontainers, references all `src/` projects) |
| [`SimpleStack.Orm.xUnit.xunit.runner.json`](SimpleStack.Orm.xUnit.xunit.runner.json) | xUnit runner settings (internal diagnostics, class-and-method display names), copied to the output folder |

## Relationships

- **Part of:** [`test/`](../README.md) of [simplestack.orm](../../README.md)
- **Tests:** every project in [`src/`](../../src/README.md)
