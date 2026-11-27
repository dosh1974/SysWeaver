# SysWeaver.Common.Linux

[⬆ SysWeaver overview](../../README.md)

> Linux implementation of the platform abstraction (`IPlatformTools`): CPU/memory metrics, reboot, directory permissions and OS identification.

| | |
|---|---|
| **Layer** | Foundation |
| **Kind** | Library (platform plug-in, loaded by type name at runtime) |
| **Platform** | Linux only |

## Purpose

The Linux counterpart of [SysWeaver.Common.Windows](../SysWeaver.Common.Windows/README.md): it lets portable framework code query CPU and memory usage, identify the OS, make shared data folders writable for everyone or reboot the machine without Linux-specific code leaking into the core.

## How it fits into SysWeaver

`PlatformTools.Current` in [SysWeaver.Common](../../SysWeaver.Common/README.md) resolves `SysWeaver.LinuxPlatformTools, SysWeaver.Common.Linux` by type name when the process runs on Linux, and falls back to `DummyPlatformTools` if the assembly is missing. No project calls it directly; [SysWeaver.Storage](../../SysWeaver.Storage/README.md) references it only so that it is copied to the output.

## Key features

- CPU usage (two samples of `/proc/stat`) and physical memory (`/proc/meminfo`), shown in the service manager's statistics
- OS friendly name from `PRETTY_NAME` in the distribution's `/etc/*-release` files; default key folder `/etc/keys`
- Directory permissions mimicking the Windows "Everyone full control (inherited)" rule: `a+rwX` on the directory and its content plus a POSIX default ACL (`setfacl`) so content created later stays writable by everyone; used by `Folders` and `PathExt.CreateDataFolder`
- Reboot via `sudo /sbin/reboot`
- Executable discovery (`FindExecutables`: ELF binaries and `#!` scripts of any name, shared libraries skipped) and `MakeExecutable` (adds the execute bit for every class that can read the file), used by [SysWeaver.MicroService.ServerManager](../../SysWeaver.MicroService.ServerManager/README.md) for deployed applications
- Network connections (`INetConnections`, see `NetConnections` in SysWeaver.Common): `/proc/net/tcp`, `tcp6`, `udp` and `udp6`, the owning process from the `socket:[inode]` links in `/proc/[pid]/fd`
- Failures counted as statistics (`IHaveStats`, system `Linux.Platform`)

## Key components

- [`LinuxPlatformTools`](LinuxPlatformTools.cs) — the `IPlatformTools` implementation, the network connections in [`LinuxPlatformTools.NetConnections.cs`](LinuxPlatformTools.NetConnections.cs)

## Using it

Deploy the DLL next to the executable; no configuration is required.

## Limitations and considerations

- Linux only; other Unix variants (e.g. macOS, FreeBSD) have no implementation and get the dummy tools
- `FlushToDisc` is a no-op that reports success (no fsync)
- `GetCpuUsage` blocks the caller for the 250 ms sampling interval
- The network connections have no creation time (Linux doesn't keep it), and the process of a socket is only found for the processes whose `/proc/[pid]/fd` can be read (all of them as root)
- Directory permissions need the `acl` package (`setfacl`) and a file system with ACL support; without it only the mode bits are applied (an exception is returned) and later content follows the creator's umask. Entries owned by other users can't be changed unless running as root
- Reboot requires password-less `sudo` for `/sbin/reboot` and only reports whether `sudo` could be started
- Data shared by all users lives in `/var/lib` (`$(CommonApplicationData)`), where only root can create folders: run the application once as root, or create `/var/lib/SysWeaver` with `sudo mkdir -p /var/lib/SysWeaver && sudo chmod 1777 /var/lib/SysWeaver`

## Relationships

- **Project:** [`SysWeaver.Common.Linux.csproj`](SysWeaver.Common.Linux.csproj)
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.Storage](../../SysWeaver.Storage/README.md) (deployment reference); at runtime by everything that uses `PlatformTools.Current`
