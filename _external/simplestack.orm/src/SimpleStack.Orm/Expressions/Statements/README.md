# Statements

[⬆ Expressions](../README.md)

> Statement models that collect the parts of a SQL statement (table, columns, conditions, grouping, ordering, paging) together with its parameters.

| | |
|---|---|
| **Project** | [SimpleStack.Orm](../../README.md) (vendored, used by SysWeaver) |
| **Kind** | Source code |

## Purpose

The untyped classes in this folder hold the raw SQL parts; the dialect provider turns them into SQL text. The [`Typed/`](Typed/README.md) wrappers fill them from lambda expressions on a mapped class, and the [`Dynamic/`](Dynamic/README.md) wrappers fill them from table and column names without a class.

## Contents

| Item | Description |
|---|---|
| [`Dynamic/`](Dynamic/README.md) | Builders for queries without a corresponding type |
| [`Typed/`](Typed/README.md) | Generic builders driven by lambda expressions on a mapped type |
| [`CountStatement.cs`](CountStatement.cs) | Abstract base for counting / selecting: columns, distinct, group by, having |
| [`DeleteStatement.cs`](DeleteStatement.cs) | DELETE statement |
| [`InsertStatement.cs`](InsertStatement.cs) | INSERT statement |
| [`SelectStatement.cs`](SelectStatement.cs) | SELECT statement: adds ordering, offset and limit |
| [`Statement.cs`](Statement.cs) | Base `Statement` plus `StatementParameter` / `StatementParameters` |
| [`UpdateStatement.cs`](UpdateStatement.cs) | UPDATE statement |
| [`WhereStatement.cs`](WhereStatement.cs) | Abstract base for statements with a WHERE clause |

## Relationships

- **Part of:** [SimpleStack.Orm](../../README.md)
- **Filled by:** the visitors in [`Expressions/`](../README.md)
