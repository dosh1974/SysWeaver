# Dynamic

[⬆ Statements](../README.md)

> Builders for SELECT and COUNT queries that address tables and columns by name, without a corresponding C# type.

| | |
|---|---|
| **Project** | [SimpleStack.Orm](../../../README.md) (vendored, used by SysWeaver) |
| **Kind** | Source code |

## Purpose

Used by the dynamic query methods of `OrmConnection` (see the upstream [Dynamic Queries](https://simplestack.org/query/select_async_dyn) documentation). They wrap a `SelectStatement` and offer a fluent API (`From`, `Distinct`, `GroupBy`, `Where`, ...); conditions are given as a column name plus a lambda on the column value.

## Contents

| Item | Description |
|---|---|
| [`DynamicCountStatement.cs`](DynamicCountStatement.cs) | Fluent builder for COUNT queries (table, distinct, group by, where, having) |
| [`DynamicSelectStatement.cs`](DynamicSelectStatement.cs) | Extends the count builder with selected columns, ordering and paging |

## Relationships

- **Part of:** [SimpleStack.Orm](../../../README.md)
- **Uses:** [`Statements/`](../README.md), `ColumnWhereExpressionVisitor` in [`Expressions/`](../../README.md)
