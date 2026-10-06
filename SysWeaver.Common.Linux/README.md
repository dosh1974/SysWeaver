# SysWeaver.Common.Linux

[⬆ SysWeaver overview](../README.md)

> Linux implementation of the platform abstraction (`IPlatformTools`): CPU/memory metrics, reboot and OS identification.

| | |
|---|---|
| **Layer** | Foundation (platform adapter) |
| **Kind** | Library, loaded by name at runtime |
| **Platform** | Linux |

## Purpose

The Linux counterpart of [SysWeaver.Common.Windows](../SysWeaver.Common.Windows/README.md): it lets portable framework code query CPU/memory, identify the OS or reboot the machine without Linux-specific code leaking into the core.

## How it fits into SysWeaver

`PlatformTools.Current` in [SysWeaver.Common](../SysWeaver.Common/README.md) resolves `SysWeaver.LinuxPlatformTools` (assembly `SysWeaver.Common.Linux`) by type name when the process runs on Linux. The OS friendly name is the `PRETTY_NAME` from the distribution's `/etc/*-release` files, and the default key folder is `/etc/keys`.

## Key features

- `LinuxPlatformTools`: CPU usage (parsed from `top -b -n 1`) and physical memory (from `/proc/meminfo`), used by the service manager's statistics.
- Reboot via `sudo /sbin/reboot`.
- Human readable distribution name.
- Exception counters exposed as statistics (`IHaveStats`).

## Limitations and considerations

- Linux only; other Unix variants (e.g. macOS, FreeBSD) have no dedicated implementation and fall back to the dummy tools.
- Must be deployed with the application to be found.
- `FlushToDisc` and `MakeDirectoryAccessableToEveryOne` are no-ops that report success (no fsync, no permission changes).
- CPU usage starts a `bash`/`top` process per call and parses the number with the current culture; reboot requires password-less `sudo` for `/sbin/reboot`.

## Using it

Deploy the DLL next to the executable; no configuration is required.

## Relationships

- **Project:** [`SysWeaver.Common.Linux.csproj`](SysWeaver.Common.Linux.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.Storage](../SysWeaver.Storage/README.md)
