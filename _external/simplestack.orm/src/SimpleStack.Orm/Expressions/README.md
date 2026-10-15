# Expressions

[⬆ SimpleStack.Orm](../README.md)

> Turns C# lambda expressions into parameterised SQL fragments and assembles them into SELECT, COUNT, INSERT, UPDATE and DELETE statements.

| | |
|---|---|
| **Project** | [SimpleStack.Orm](../README.md) (vendored, used by SysWeaver) |
| **Kind** | Source code |

## Purpose

The `OrmConnection` methods (e.g. `Select<T>(x => ...)`, `DeleteAll<T>(x => ...)`) build a statement object from [`Statements/`](Statements/README.md) and use the expression visitors in this folder to translate the lambdas into SQL with named parameters. The dialect provider then renders the final SQL text.

## Contents

| Item | Description |
|---|---|
| [`Statements/`](Statements/README.md) | Statement models (untyped, typed and dynamic) holding the parts of a SQL statement and its parameters |
| [`ColumnWhereExpressionVisitor.cs`](ColumnWhereExpressionVisitor.cs) | Visitor for WHERE conditions on a named column (used by the dynamic statements) |
| [`ExpressionVisitor.cs`](ExpressionVisitor.cs) | Abstract base visitor: walks an expression tree and emits SQL and parameters |
| [`Sql.cs`](Sql.cs) | `Sql` marker functions usable inside lambdas (`Desc`, `As`, `Sum`, ...) that translate to SQL constructs |
| [`TableFieldsExpresionVisitor.cs`](TableFieldsExpresionVisitor.cs) | Visitor for field lists (selected columns, ordering, grouping) |
| [`TableWhereExpresionVisitor.cs`](TableWhereExpresionVisitor.cs) | Visitor for WHERE conditions on a mapped type |

## Relationships

- **Part of:** [SimpleStack.Orm](../README.md)
- **Uses:** the dialect provider (`IDialectProvider`) for quoting, functions and parameter naming
