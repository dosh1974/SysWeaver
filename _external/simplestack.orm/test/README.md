# test

[⬆ simplestack.orm](../README.md)

> Upstream test projects of SimpleStack.Orm, carried along unused.

| | |
|---|---|
| **Library** | [simplestack.orm](../README.md) (vendored) |
| **Kind** | Group of test projects |

## Purpose

Both projects target `netcoreapp3.1` and reference the core and all dialect projects in [`src/`](../src/README.md). Neither is part of `SysWeaver.slnx`, so they are not built or run by SysWeaver. SysWeaver's own tests (in the separate SysWeaver.Tests repository) only reach the ORM through SysWeaver.DbSimpleStack.

## Contents

| Item | Used by SysWeaver | Description |
|---|---|---|
| [`SimpleStack.Orm.Tests/`](SimpleStack.Orm.Tests/README.md) | No | NUnit tests of SQL generation, CRUD, transactions and schema queries across dialects |
| [`SimpleStack.Orm.xUnit/`](SimpleStack.Orm.xUnit/README.md) | No | xUnit tests (table creation) run against connection factories for every dialect |

## Relationships

- **Part of:** [simplestack.orm](../README.md), vendored under [_external](../../README.md)
- **Tests:** [`src/`](../src/README.md)
