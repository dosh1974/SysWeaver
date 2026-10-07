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
- `FlushToDisc` is a no-op that reports success (no fsync).
- `MakeDirectoryAccessableToEveryOne` mimics the Windows "Everyone full control (inherited)" rule, so data created by a service running as root stays modifiable by users and vice versa: the directory and all existing content get `a+rwX` (directories also get setgid, never the sticky bit; symbolic links are skipped), and a POSIX default ACL (`setfacl -R -P -d -m u::rwx,g::rwx,o::rwx,m::rwx`) makes files and folders created later writable by everyone regardless of the creator's umask. Requires the `acl` package (`setfacl`) and a file system with ACL support; without it the mode bits are still applied but an exception is returned (new content then follows the creator's umask). Entries not owned by the process user (unless root) can't be changed and are reported in the returned exception. Successfully processed directories are cached per process. Used by `Folders` (all-user folders) and `PathExt.AllowAllAccess` / `PathExt.CreateDataFolder`.
- CPU usage starts a `bash`/`top` process per call and parses the number with the current culture; reboot requires password-less `sudo` for `/sbin/reboot`.

## Using it

Deploy the DLL next to the executable; no configuration is required.

## Relationships

- **Project:** [`SysWeaver.Common.Linux.csproj`](SysWeaver.Common.Linux.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.Storage](../SysWeaver.Storage/README.md)
