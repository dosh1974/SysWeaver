# Edit

[⬆ SysWeaver.TableData](../README.md)

> Table editing: operations that append, merge, select, remove, compute, sort and filter columns and rows of existing table data, plus the column aggregation code that is not exposed yet.

| | |
|---|---|
| **Project** | [SysWeaver.TableData](../README.md) |
| **Kind** | Source code |

## Purpose

`TableDataEdit` works on `BaseTableData`, usually a table stored as a data reference (see [`DataReferenceStorage.cs`](../DataReferenceStorage.cs)). Every method returns a new table and never modifies the source, although cell values and column objects are shared. `EditTableDataRequest` carries a list of `TableDataOp` steps. `TableDataEdit.ApplyOps` applies them in order, and each step produces a new table. The HTTP server exposes this as the `EditTableData` web API / AI tool.

The `InternalTableAggregator*` classes and `TableColumnAggregation(s)` prepare per-type aggregation functions, but `TableDataEdit.Aggregate` is not implemented yet (it throws `NotImplementedException`).

## Contents

| Item | Description |
|---|---|
| [`TableDataEdit.cs`](TableDataEdit.cs) | `TableDataEdit`: `ApplyOps`, `Append`, `MergeColumns`, `AddColumns`, `SelectColumns`, `RemoveColumns`, `Filter`, `Reverse`, `Aggregate` (not implemented) |
| [`TableDataOp.cs`](TableDataOp.cs) | One edit step: append tables by reference, merge columns, compute columns, select or remove columns, sort and filter rows |
| [`TableDataMergeOp.cs`](TableDataMergeOp.cs) | Merge columns from another table with the same number of rows |
| [`EditTableDataRequest.cs`](EditTableDataRequest.cs) | `EditTableDataRequest` (data reference + ops) and `GetTableDataRequest` |
| [`TableColumnAggregation.cs`](TableColumnAggregation.cs) | Describes the aggregation of one column (for the future `Aggregate`) |
| [`TableColumnAggregations.cs`](TableColumnAggregations.cs) | Aggregation kinds: `SelectFirst`, `SelectLast`, `Count`, `Min`, `Max`, `Sum`, `Average` |
| [`InternalTableAggregator.cs`](InternalTableAggregator.cs) | Registry of aggregation functions per value type (numeric types, `DateTime`, `TimeSpan`, `String` pre-registered) |
| [`InternalTableAggregatorT.cs`](InternalTableAggregatorT.cs) | Builds aggregation functions for other types (invoked through reflection) |
| [`InternalTableAggregatorType.cs`](InternalTableAggregatorType.cs) | The set of aggregation functions for one value type |
| [`InternalHelpers.cs`](InternalHelpers.cs) | Generic aggregation helpers |

## Relationships

- **Part of:** [SysWeaver.TableData](../README.md)
- **Uses:** [`NewTableDataColumn.cs`](../NewTableDataColumn.cs) (computed columns), [SysWeaver.ExpressionEvaluator](../../SysWeaver.ExpressionEvaluator/README.md)
- **Used by:** [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md) (`EditTableData` API)
