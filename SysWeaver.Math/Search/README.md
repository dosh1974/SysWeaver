# Search

[⬆ SysWeaver.Math](../README.md)

> A genetic-algorithm solver / optimiser.

| | |
|---|---|
| **Project** | [SysWeaver.Math](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.Search`) |

## Purpose

`GaSolver.SolveOrOptimize` evolves a population of states to minimise an error. The problem is described by implementing `IGaOperators<T>`: deep clone a state, mutate it (minimum amount and per generation), compute its error and decide when to abort (also usable to report progress). The solver can run the population in parallel on all cores and returns a `GaResult`.

## Contents

| Item | Description |
|---|---|
| [`GaSolver.cs`](GaSolver.cs) | The solver (`SolveOrOptimize`) and its internal population type |
| [`IGaOperators.cs`](IGaOperators.cs) | The operations the user implements on the state (clone, mutate, error, abort) |
| [`GaResult.cs`](GaResult.cs) | The result of a run: final error, generations run, generations without improvement |

## Relationships

- **Part of:** [SysWeaver.Math](../README.md)
- **Related:** [Geometry/](../Geometry/README.md)
