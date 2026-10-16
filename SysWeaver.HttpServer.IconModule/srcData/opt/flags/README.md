# flags

[⬆ opt](../README.md)

> Optimised copies of the flag icons in `srcData/flags/`; not used by the build.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer.IconModule](../../../README.md) |
| **Kind** | Icons |

## Purpose

The files have the same names as those in [`srcData/flags/`](../../flags/README.md), with the `id` attributes and white space removed, which matches the output of `SvgOpt.exe -RemoveSize`. The build regenerates this kind of output in the git-ignored `srcData\.opt\flags\` and embeds that instead, so these files are not embedded or served.

## Contents

| Item | Description |
|---|---|
| `*.svg` (270 files) | One per file in [`srcData/flags/`](../../flags/README.md), e.g. [`dk.svg`](dk.svg), [`us.svg`](us.svg), [`gb-eng.svg`](gb-eng.svg), [`arab.svg`](arab.svg) |

## Relationships

- **Part of:** [SysWeaver.HttpServer.IconModule](../../../README.md)
- **Copy of:** [`srcData/flags/`](../../flags/README.md)
