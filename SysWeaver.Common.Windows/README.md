# SysWeaver.Common.Windows

[⬆ SysWeaver overview](../README.md)

> Windows implementation of the platform abstraction (`IPlatformTools`): system metrics, reboot, disk flush and directory permissions.

| | |
|---|---|
| **Layer** | Foundation (platform adapter) |
| **Kind** | Library, loaded by name at runtime |
| **Platform** | Windows |

## Purpose

Keeps Windows-only APIs (performance counters, WMI, Win32 shutdown/ACL calls) out of the portable core. The core asks `PlatformTools.Current` for an implementation and gets this one when running on Windows.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Caller["Framework code"] --> PT["PlatformTools.Current"]
  PT -->|Windows: load by type name| Win["WindowsPlatformTools"]
  PT -->|Linux| Lin["LinuxPlatformTools"]
  PT -->|not found| Dummy["DummyPlatformTools"]
```

No project calls this assembly directly; it only needs to be deployed with the application (the storage layer references both platform assemblies so they are copied to the output automatically).

## Key features

- `WindowsPlatformTools`: CPU usage (performance counter `Processor Information\% Processor Time\_Total`) and physical memory (`GlobalMemoryStatusEx`), surfaced in the service manager's statistics tables.
- OS friendly name from WMI (`Win32_OperatingSystem` caption, build and bitness).
- Machine reboot via `ExitWindowsEx` after enabling `SeShutdownPrivilege` (used by administrative services).
- Flushing file handles to disk (`FlushFileBuffers`) and granting the Everyone group full control (inherited by contained files and folders).
- Default key folder location for the platform (`C:\Keys`).
- Exception counters exposed as statistics (`IHaveStats`).

## Limitations and considerations

- Only meaningful on Windows; relies on Windows management and performance-counter APIs.
- If the assembly is missing, or the performance counter / WMI query fails while the instance is created, the framework silently falls back to a dummy implementation (metrics become unavailable rather than failing).

## Using it

Nothing to configure — deploy the DLL with the application and use `PlatformTools.Current`.

## Relationships

- **Project:** [`SysWeaver.Common.Windows.csproj`](SysWeaver.Common.Windows.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.Storage](../SysWeaver.Storage/README.md)
