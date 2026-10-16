# Typed

[⬆ Statements](../README.md)

> Generic statement builders that fill a SQL statement from lambda expressions on a mapped type `T`.

| | |
|---|---|
| **Project** | [SimpleStack.Orm](../../../README.md) (vendored, used by SysWeaver) |
| **Kind** | Source code |

## Purpose

These are the objects passed to the configuration lambdas of the `OrmConnection` methods, e.g. `conn.Select<Dog>(x => { x.Where(...); x.OrderBy(...); })`. Each wraps one untyped statement from [`Statements/`](../README.md) (exposed as its `Statement` property) and uses the expression visitors to translate the lambdas.

## Contents

| Item | Description |
|---|---|
| [`TypedDeleteStatement.cs`](TypedDeleteStatement.cs) | DELETE on `T`, optionally restricted to the primary key of a given row |
| [`TypedInsertStatement.cs`](TypedInsertStatement.cs) | INSERT into `T`: target table, values (optionally only some fields), ignore errors |
| [`TypedSelectStatement.cs`](TypedSelectStatement.cs) | SELECT on `T`: columns, distinct, aggregation, where, group by, having, ordering, limit |
| [`TypedUpdateStatement.cs`](TypedUpdateStatement.cs) | UPDATE on `T`: target table, values (optionally only some fields) |
| [`TypedWhereStatement.cs`](TypedWhereStatement.cs) | Abstract base with WHERE / AND / OR handling |

## Relationships

- **Part of:** [SimpleStack.Orm](../../../README.md)
- **Uses:** [`Statements/`](../README.md), the visitors in [`Expressions/`](../../README.md)
