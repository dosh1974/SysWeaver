# SysWeaver.Common.Linux

[⬆ SysWeaver overview](../README.md)

> Linux implementation of the platform abstraction (`IPlatformTools`): system metrics, reboot, disk flush, directory permissions and OS identification.

| | |
|---|---|
| **Layer** | Foundation (platform adapter) |
| **Kind** | Library, loaded by name at runtime |
| **Platform** | Linux |

## Purpose

The Linux counterpart of [SysWeaver.Common.Windows](../SysWeaver.Common.Windows/README.md): it lets portable framework code query CPU/memory, reboot the machine or fix directory permissions without Linux-specific code leaking into the core.

## How it fits into SysWeaver

`PlatformTools.Current` in [SysWeaver.Common](../SysWeaver.Common/README.md) resolves this implementation by type name when the process runs on Linux. The OS friendly name is read from the distribution's os-release information, and the default key folder follows Linux conventions.

## Key features

- CPU and memory usage and system statistics for the service manager.
- Reboot, flush-to-disk and directory permission helpers.
- Human readable distribution name.

## Limitations and considerations

- Linux only; other Unix variants (e.g. macOS, FreeBSD) have no dedicated implementation and fall back to the dummy tools.
- Must be deployed with the application to be found.

## Using it

Deploy the DLL next to the executable; no configuration is required.

## Relationships

- **Project:** [`SysWeaver.Common.Linux.csproj`](SysWeaver.Common.Linux.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.Storage](../SysWeaver.Storage/README.md)
