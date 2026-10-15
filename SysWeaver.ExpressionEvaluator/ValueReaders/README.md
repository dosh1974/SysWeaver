# ValueReaders

[⬆ SysWeaver.ExpressionEvaluator](../README.md)

> Number literal readers used by the tokenizer, one per evaluator type.

| | |
|---|---|
| **Project** | [SysWeaver.ExpressionEvaluator](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.Parser.ValueReader`) |

## Purpose

When the tokenizer finds a number in an expression it hands it to the value reader of the evaluator's type, which parses it (by default with the invariant culture) and creates the value token. `ValueReader` adds parsing of prefixed integer literals: hexadecimal (`0x`), octal (`0o`) and binary (`0b`).

## Contents

| Item | Description |
|---|---|
| [`ValueReader.cs`](ValueReader.cs) | Prefix detection and parsing of hexadecimal, octal and binary integer values |
| [`DoubleValueReader.cs`](DoubleValueReader.cs) | Reads `Double` values (`NumberStyles.Float` by default) |
| [`DecimalValueReader.cs`](DecimalValueReader.cs) | Reads `Decimal` values (`NumberStyles.Float` by default) |
| [`Int64ValueReader.cs`](Int64ValueReader.cs) | Reads `Int64` values (`NumberStyles.Integer` by default) |
| [`UInt64ValueReader.cs`](UInt64ValueReader.cs) | Reads `UInt64` values (`NumberStyles.Integer` by default) |

## Relationships

- **Part of:** [SysWeaver.ExpressionEvaluator](../README.md)
- **Used by:** the standard evaluators in [`ExpressionEvaluator.cs`](../ExpressionEvaluator.cs)
