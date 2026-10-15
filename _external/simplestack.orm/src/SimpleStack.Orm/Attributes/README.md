# Attributes

[⬆ SimpleStack.Orm](../README.md)

> Attributes placed on row classes and their properties to control how SimpleStack.Orm maps them to tables and columns.

| | |
|---|---|
| **Project** | [SimpleStack.Orm](../README.md) (vendored, used by SysWeaver) |
| **Kind** | Source code |

## Purpose

`ConfigExtensions.cs` reads these attributes when it builds a `ModelDefinition`; the dialect providers use the result when creating tables and generating SQL. SysWeaver row classes use them directly, and [SysWeaver.DbSimpleStack](../../../../../SysWeaver.DbSimpleStack/README.md) adds further attributes to the same `SimpleStack.Orm.Attributes` namespace.

## Contents

| Item | Description |
|---|---|
| [`AliasAttribute.cs`](AliasAttribute.cs) | Alternative table or column name; the same file also declares `PartitionByKeyAttribute`, `PartitionByHashAttribute` and `PrimaryKeyTypeAttribute` (with the `MySqlPrimaryKeyTypes` enum `BTree` / `Hash`); the partition attributes are read by SysWeaver.DbSimpleStack.MySql, the primary key type ends up as `USING <type>` in the generated primary key definition |
| [`AutoincrementAttribute.cs`](AutoincrementAttribute.cs) | Auto-incrementing column |
| [`CompositeIndexAttribute.cs`](CompositeIndexAttribute.cs) | Index over several columns (on the class) |
| [`ComputeAttribute.cs`](ComputeAttribute.cs) | Computed column (SQL expression) |
| [`DecimalLengthAttribute.cs`](DecimalLengthAttribute.cs) | Precision and scale of a decimal column |
| [`DefaultAttribute.cs`](DefaultAttribute.cs) | Default column value |
| [`ForeignKeyAttribute.cs`](ForeignKeyAttribute.cs) | Foreign key (a `ReferencesAttribute`) with name and `OnDelete` / `OnUpdate` options |
| [`IgnoreAttribute.cs`](IgnoreAttribute.cs) | Property that is not mapped to a column |
| [`IndexAttribute.cs`](IndexAttribute.cs) | Index (optionally unique) on a column |
| [`PrimaryKeyAttribute.cs`](PrimaryKeyAttribute.cs) | Primary key column |
| [`ReferencesAttribute.cs`](ReferencesAttribute.cs) | Reference to another type (base of `ForeignKeyAttribute`) |
| [`RequiredAttribute.cs`](RequiredAttribute.cs) | Non-nullable column |
| [`SchemaAttribute.cs`](SchemaAttribute.cs) | Database schema of the table |
| [`SequenceAttribute.cs`](SequenceAttribute.cs) | Named sequence used to generate values |
| [`StringLengthAttribute.cs`](StringLengthAttribute.cs) | Maximum string length |
| [`UpdateAggregateAttribute.cs`](UpdateAggregateAttribute.cs) | Aggregation applied to a column on upsert (see `UpdateAggregations`) |

## Relationships

- **Part of:** [SimpleStack.Orm](../README.md)
- **Used by:** [SysWeaver.DbSimpleStack](../../../../../SysWeaver.DbSimpleStack/README.md) and the row classes of SysWeaver services
