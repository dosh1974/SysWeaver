# Platform

[⬆ SysWeaver overview](../README.md)

> Groups the OS-specific projects: platform tools and `ServiceHost` back-ends for Windows and Linux.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Group of projects |

## Purpose

The core projects stay OS-neutral and delegate OS-specific work to the projects in this folder: `IPlatformTools` (defined in [SysWeaver.Common](../SysWeaver.Common/README.md)) for system metrics, reboot and directory permissions, and the service host factories of [SysWeaver.OsServices](../SysWeaver.OsServices/README.md) for installing and running an application as a Windows service or a systemd unit. Each OS has one project of each kind. The folder itself only holds the project folders.

## Contents

| Item | Description |
|---|---|
| [`SysWeaver.Common.Linux/`](SysWeaver.Common.Linux/README.md) | Linux implementation of the platform abstraction (`IPlatformTools`): CPU/memory metrics, reboot, directory permissions and OS identification. |
| [`SysWeaver.Common.Windows/`](SysWeaver.Common.Windows/README.md) | Windows implementation of the platform abstraction (`IPlatformTools`): system metrics, reboot, disk flush and directory permissions. |
| [`SysWeaver.OsServices.ServiceHostFactoryUnix/`](SysWeaver.OsServices.ServiceHostFactoryUnix/README.md) | Linux back-end for `ServiceHost`: installs the application as a systemd unit (or a SysVinit script on systems without systemd). |
| [`SysWeaver.OsServices.ServiceHostFactoryWin32NT/`](SysWeaver.OsServices.ServiceHostFactoryWin32NT/README.md) | Windows back-end for `ServiceHost`: installs, configures and runs the application as a Windows service. |

## Relationships

- **Part of:** [SysWeaver](../README.md)
- **Implements contracts of:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.OsServices](../SysWeaver.OsServices/README.md)
