# opt

[⬆ srcData](../README.md)

> A tracked folder of SvgOpt-optimised data icons; the current build does not use it.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IconModule](../../README.md) |
| **Kind** | Icons |

## Purpose

The build writes its optimised output to `srcData\.opt\` (with a leading dot, git-ignored by the `*.opt` rule) and compresses that folder. This `opt/` folder (no dot) is not referenced by the `.csproj` or the code; it appears to be a leftover copy of earlier optimisation output.

## Contents

| Item | Description |
|---|---|
| [`flags/`](flags/README.md) | Optimised copies of the 270 flag icons in [`../flags/`](../flags/README.md) |

## Relationships

- **Part of:** [SysWeaver.HttpServer.IconModule](../../README.md)
- **Related:** [`srcData/`](../README.md), whose build output (`.opt/`) has the same layout
