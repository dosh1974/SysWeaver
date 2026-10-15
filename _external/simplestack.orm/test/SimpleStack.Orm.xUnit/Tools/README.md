# Tools

[⬆ SimpleStack.Orm.xUnit](../README.md)

> Helper that provides an `OrmConnectionFactory` per dialect for xUnit theories.

| | |
|---|---|
| **Project** | [SimpleStack.Orm.xUnit](../README.md) (vendored, not used by SysWeaver) |
| **Kind** | Source code (tests) |

## Purpose

An alternative to the container-based `BaseTest` fixture: the factories point at database servers on `localhost` with hard-coded upstream test credentials, plus two SQLite files in the temp folder.

## Contents

| Item | Description |
|---|---|
| [`ConnectionFactories.cs`](ConnectionFactories.cs) | Static list of connection factories for MySQL, MySqlConnector, SQL Server, PostgreSQL and both SQLite dialects, exposed as xUnit member data (`All`) |
